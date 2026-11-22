# 0011. Concurrency control: row locks for balances, optimistic concurrency for transfers

- **Status:** Accepted
- **Date:** 2026-11-23

## Context

Two requests that read the same balance at the same time can both pass the funds check and both write: a customer with 50 TL sends 40 TL twice and ends with 10 TL (a **lost update**). The `PostingEngine` is correct only if the balances it sees are current and nobody changes them before the entry is saved.

A transfer's status has a different, much rarer conflict: the request and the recovery job ([ADR 0014](0014-synchronous-transfers-and-recovery.md)) may finish the same transfer at the same time. A late write must not silently overwrite the outcome that is already stored.

## Decision

- **Balances: pessimistic locking.** `Post` runs in one transaction at Read Committed. It first locks every account of the entry with `SELECT ... FOR UPDATE`, then loads them, runs the `PostingEngine`, saves the entry and the balances, and commits. A concurrent post on the same account waits and then sees the new balance. The locks live exactly as long as that transaction.
- **Deadlocks are prevented by lock order:** accounts are always locked in ascending id order, so two entries that touch the same accounts wait for each other in the same order.
- **Transfer status: optimistic concurrency** with PostgreSQL's `xmin` system column as the row version (`IsRowVersion()` on a shadow property). Every update carries `WHERE xmin = <version read>`; if another writer changed the row, no row matches and EF Core raises `DbUpdateConcurrencyException`. The repository turns that into `false`, and the caller reads the transfer again and reports the stored outcome.
- The poster refuses to run if accounts are already tracked by its context, because the query after the lock would return those stale instances.

## Alternatives considered

- **Optimistic concurrency for balances:** no waiting, but under contention every conflict is an error and a retry. The bank's own accounts (cash, fee income) take part in many entries, so they would conflict constantly.
- **Pessimistic locking for transfer status:** correct, but a transfer row would be locked for the whole request, including the Ledger call, to protect against a conflict that happens only after a crash.
- **Serializable isolation:** the database detects the conflict, but every caller needs retry logic and the hot-account problem is the same.
- **A conditional update** (`UPDATE ... SET balance = balance - 40 WHERE balance >= 40`): fast, but the funds rule moves into SQL and bypasses the `PostingEngine` (accumulation over several postings, all or nothing).
- **Letting PostgreSQL detect deadlocks and retrying:** works, but aborts transactions that a fixed lock order never puts at risk.

## Consequences

- Concurrent posts on the same account are serialized. Hot accounts (cash, fee income) become a queue; at this scale that is acceptable. Banks with high volume split such accounts or update their balances in batches.
- `xmin` needs no column of its own: PostgreSQL keeps it on every row, so the migration declares it but creates nothing.
- Tests prove both strategies: 100 concurrent 1 TL transfers from 50 TL complete exactly 50 times and leave exactly 0, both against the Ledger directly and through HTTP; transfers in opposite directions run concurrently without deadlocks; a stale save of a transfer loses and the stored outcome stands (`TransferVersionTests`, the only test that fails without `xmin`).
- Single-threaded tests cannot see a missing lock or a lock taken outside a transaction. Every locking change needs a concurrent test.
