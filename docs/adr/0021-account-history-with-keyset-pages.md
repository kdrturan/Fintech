# 0021. Account history: keyset pages ordered by posting id

- **Status:** Accepted
- **Date:** 2026-11-27

## Context

A customer could see an account's balance but not how it came about. Two gaps were left open in phase 2:

- **After a replayed 500 on a transfer** the client could not tell whether the money had moved ([ADR 0012](0012-idempotency.md)).
- **The recipient** of a transfer had no way to see the money arrive.

Customers also had no way to find their own accounts again.

An account's history only grows and can be long: a busy account has hundreds of thousands of postings. It must be read in pages, and a page must not change under a customer who is reading while money arrives.

## Decision

- **`GET /accounts`** returns the caller's accounts, ordered by id. The owner is part of the query ([ADR 0019](0019-authorization-roles-and-account-ownership.md)).
- **`GET /accounts/{id}/entries?limit=20&after=<cursor>`** returns the account's postings, newest first. The owner is checked on every page.
  - Each entry has: the journal entry's id (both sides of a transfer see the same one), the `operationId` that caused it (for a transfer, the transfer's id), a signed amount (positive when money came in), the currency and `createdAt`.
  - The amount's sign comes from `Account.EffectOf`, the same method that changes the balance, so the entries always add up to the balance. Debit and credit are the bank's words, not the customer's.
- **Keyset paging, not offset.** The page is `WHERE account_id = @id AND id < @cursor ORDER BY id DESC`.
  - `limit + 1` rows are read: the extra one says whether there is a next page and is not returned. No `COUNT(*)`.
  - `limit` is 1 to 100 (default 20).
  - The response is an object, `{ "items": [...], "nextCursor": "..." }`, so fields can be added later. `nextCursor` is null on the last page.
- **The order is `postings.id`, not `created_at`.** The phase 3 plan said `(created_at, id)`; we changed it while building:
  - In PostgreSQL `now()` is the start of the transaction. A transfer that waits for an account lock is written after a deposit that finished meanwhile, but carries an earlier time. A client asking for "everything newer than what I saw" would never see it. A test holds a lock to force exactly this.
  - A posting's id is taken while its account is locked ([ADR 0011](0011-concurrency-control.md)). Within one account, a later change always has a larger id, so the order is the order in which the balance changed.
  - `created_at` is in `journal_entries` and the account id is in `postings`, so no single index covers both.
- **The cursor** is the last posting id the client saw: eight bytes, big-endian, Base64Url. It is **opaque**: clients pass it back without reading it, and its content can change. It is not encrypted or signed. It is a position, not a permission, which is why ownership is checked on every page. A cursor that does not decode is a 400 (`ledger.invalid_cursor`), never treated as the first page.
- **An index on `(account_id, id)`** replaces the one on `(account_id)`. It does the old index's work too, because its first column is the same.
- The first page and later pages are two different queries, built in code. One query with `@after IS NULL OR id < @after` returns the same rows, but PostgreSQL cannot use the index range with it.

Measured on 2 million postings (200,000 on the busy account, 100 on a quiet one, all written at the very start):

| Query | Time |
|---|---|
| Busy account, deep page with `OFFSET 190000` | 210 ms |
| Busy account, deep page, keyset on `(created_at, id)` | 157 ms |
| Busy account, deep page, keyset on `id` | 0.13 ms |
| Busy account, deep page, `@after IS NULL OR id < @after` (generic plan) | 42 ms |
| Quiet account, first page, index on `(account_id)` | 236 ms |
| Quiet account, first page, index on `(account_id, id)` | 0.13 ms |

## Alternatives considered

- **Offset (`?page=5`):** simple, and allows jumping to a page. It reads and discards every skipped row. Worse, a posting that arrives while the customer reads moves every row down one: the next page starts with a row already shown. On a statement that looks like a double charge.
- **Keyset on `(created_at, id)`:** the original plan. Slower, and in the wrong order for transfers that waited for a lock.
- **A sequence number per account:** the cleanest order, and the cursor would reveal nothing about other accounts. It needs a counter on every account, updated in every posting.
- **An encrypted or signed cursor:** hides the posting id. Ownership would still have to be checked, so it buys privacy, not security.

## Consequences

- After a replayed 500 the customer looks at the entries. If the money moved, the entry's `operationId` is the transfer id, and `GET /transfers/{id}` gives the outcome.
- The order is guaranteed **within one account only**. Ids are taken from one counter for the whole bank and are not in commit order across accounts. A feed of all postings could not use this cursor.
- `createdAt` in a list may not be strictly ordered (the lock case above). The list order is the truth.
- There is no "go to page 57". A statement does not need it.
- **Known limitations:**
  - **The cursor reveals roughly how many postings the bank makes:** the posting id is one counter for all accounts. It holds no amounts and no names.
  - **No counterparty and no description:** the recipient sees the amount but not who sent it. Names come with the Customers module, under KVKK.
  - No balance after each entry, and no date filter.
  - `GET /accounts` is not paged. A customer can open any number of accounts, so in theory the list can grow.
  - **If no money moved before a 500, nothing shows,** and the customer cannot tell "did not happen" from "not yet" (a `Pending` transfer the recovery job will finish). There is no list of transfers.
  - **The migration blocks writes:** `CREATE INDEX` locks `postings` against inserts while it runs. In production it must be `CREATE INDEX CONCURRENTLY`.
