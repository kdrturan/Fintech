# 0019. Authorization: roles for what you may do, ownership for what is yours

- **Status:** Accepted
- **Date:** 2026-11-27

## Context

Knowing who is calling ([ADR 0017](0017-identity-with-aspnet-core-identity-and-own-jwts.md)) is not enough. A signed-in customer must not pay cash into their own account, which would create money from nothing. They also must not read or spend someone else's account by changing an id in the URL.

That second hole is **BOLA** (Broken Object Level Authorization), first in the OWASP API Security Top 10 (2023). A role cannot close it: the answer depends on which account it is and who owns it.

## Decision

**Roles:**

- `Customer`: given at registration. Opens accounts, sees them and moves money out of them.
- `Teller`: pays cash into any customer account. Registration never grants it. A teller is created at startup from the `SeedTeller` configuration (`TellerSeeder`).
  - An existing user is never promoted to teller; the seeder logs an error instead. Anyone can register an address as a customer, and whoever registers the teller's address first must not be handed the counter.
  - If another instance or a registration takes the address between the check and the insert, the seeder looks again and still does not promote.
- Checked with `[Authorize(Roles = ...)]`: `POST /accounts` is for customers, `POST /accounts/{id}/deposits` for tellers. The role names live in `Identity.Contracts`.

**Ownership:**

- The Ledger's `Account` has an `OwnerId` (`ledger.accounts.owner_id`, indexed). The bank's own accounts have none. It is set from the token when the account is opened. There is no foreign key into the `identity` schema ([ADR 0003](0003-modular-monolith-with-ledger-as-posting-engine.md)): the Ledger knows the owner as an id and nothing more.
- **It is checked in the Application layer and the Ledger, not in HTTP:**
  - `GET /accounts/{id}` and `GET /accounts/{id}/entries`: the handler checks the owner, on every page ([ADR 0021](0021-account-history-with-keyset-pages.md)).
  - `GET /accounts`: the owner is part of the query, so nobody else's rows are read at all. List endpoints filter in the query instead of checking afterwards.
  - **Transfers:** `ILedgerApi.TransferAsync` takes the requester and checks that the source is theirs. The check sits where the money moves, so no caller can skip it, the recovery job included. That is why a transfer stores its `RequestedBy` ([ADR 0014](0014-synchronous-transfers-and-recovery.md)).
    - The destination may be anyone's customer account, since that is what sending money means. The bank's own accounts are not a valid destination.
  - `GET /transfers/{id}`: only the customer who requested the transfer.
- **Someone else's account or transfer gets 404**, the same answer as one that does not exist. A 403 would confirm that the id exists, which helps anyone guessing ids.
- Tests: every endpoint has a test in which one customer tries to reach another's account or transfer, with real tokens.

## Alternatives considered

- **An ownership table in the Identity module:** the Ledger would not change, but every request would query two modules, and opening an account would be two writes in two schemas, the same consistency problem as a transfer.
- **A Customers module:** the right long-term home for customers and KYC. Too much for phase 3.
- **Resource-based authorization in ASP.NET Core (`IAuthorizationService`):** idiomatic, but the rule stays in the HTTP layer, and the recovery job, which has no HTTP request, would never see it.
- **403 for someone else's resource:** more honest about why, but it confirms that the id exists.
- **Letting customers pay cash in:** convenient for a demo, but they could create money.

## Consequences

- Accounts opened before ownership existed have no owner, so nobody can reach them through the API.
- **Order inside the Ledger:** ownership is checked before the idempotency key. A transfer still `Pending` when the migration ran (it has no real requester) fails even if its money had moved. Pending transfers must be finished before such a release.
- **Known limitations:**
  - **401 and 403 from the authorization middleware have no `errorCode`.** They are problem details, but [ADR 0013](0013-http-api-layer.md) promises an `errorCode` on every error.
  - **The teller who paid cash in is not recorded** on the journal entry. A real bank needs it; the audit log comes with phase 4.
  - The recipient of a transfer sees its `operationId` in their account history, but `GET /transfers/{id}` answers them 404.
  - A deposit's response shows the teller the account's balance.
  - No joint accounts, no power of attorney, no screen to manage roles.
  - The seeder's race branch (two instances starting for the first time together) is not covered by a test.
