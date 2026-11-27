# Roadmap

Each phase ends with working, tested software and the ADRs for the decisions it required.

| Phase | Scope | Status |
|---|---|---|
| 0 | Solution skeleton, build rules, architecture tests, local PostgreSQL, CI | Done |
| 1 | **Domain:** `Money`, `Currency`, accounts, balanced journal entries, transfer workflow; unit tests | Done |
| 2 | **Persistence and API:** EF Core, transactions and isolation, idempotent `Post` and idempotent HTTP API, optimistic concurrency, concurrent-transfer integration tests, stuck-transfer recovery | Done |
| 3 | **Identity:** registration, JWT access and refresh tokens, roles, account ownership authorization, per-user idempotency keys, account history | Done |
| 4 | **Messaging:** transactional outbox, RabbitMQ, idempotent consumers, Notifications module, audit log | Next |
| 5 | **Operations:** Serilog, OpenTelemetry, Redis, rate limiting, daily ledger reconciliation job | |
| 6 | **Extraction:** Notifications as a separate service, deployment docs, architecture diagrams | |

Later modules (Customers/KYC, Payments) slot in after phase 3 and reuse the Ledger's posting API.

## Phase 2 in numbers

Five endpoints, three database schemas, 158 tests (98 unit, 14 architecture, 46 integration). ADRs [0010](adr/0010-persistence-with-ef-core.md) to [0016](adr/0016-integration-test-database-per-class.md).

## Phase 3 in numbers

Twelve endpoints, four database schemas (`identity` is new), 263 tests (112 unit, 19 architecture, 132 integration). ADRs [0017](adr/0017-identity-with-aspnet-core-identity-and-own-jwts.md) to [0021](adr/0021-account-history-with-keyset-pages.md); [0020](adr/0020-idempotency-keys-per-user.md) amends [0012](adr/0012-idempotency.md).

Closed from phase 2: idempotency keys are per user, `POST /accounts` is idempotent, and after a replayed 500 the customer sees in the account history whether the money moved.

## Carried forward

Known limitations, each recorded in its ADR, and the phase that picks it up:

| Limitation | ADR | Phase |
|---|---|---|
| Idempotency keys never expire; a key held by a process that died answers 409 until it is cleaned up | [0012](adr/0012-idempotency.md) | 5 (cleanup job) |
| One transfer that always throws blocks its recovery batch | [0014](adr/0014-synchronous-transfers-and-recovery.md) | 5 |
| Recovery settings are not validated at startup | [0014](adr/0014-synchronous-transfers-and-recovery.md) | small follow-up |
| Stored balances are not yet reconciled against the postings | [0009](adr/0009-double-entry-ledger-model.md) | 5 (reconciliation job) |
| Sign-in can be probed: answer timing and lockout reveal which e-mails exist, and a stranger can lock a customer out | [0017](adr/0017-identity-with-aspnet-core-identity-and-own-jwts.md) | 5 (rate limiting) |
| No two-factor authentication | [0017](adr/0017-identity-with-aspnet-core-identity-and-own-jwts.md) | later |
| Used and expired refresh tokens are never deleted | [0018](adr/0018-access-and-refresh-tokens.md) | 5 (cleanup job) |
| Sessions have no absolute lifetime | [0018](adr/0018-access-and-refresh-tokens.md) | small follow-up |
| One signing key and no key ids, so rotating it signs everyone out | [0018](adr/0018-access-and-refresh-tokens.md) | 6 |
| 401 and 403 from the authorization middleware have no `errorCode` | [0019](adr/0019-authorization-roles-and-account-ownership.md) | small follow-up |
| The teller who paid cash in is not recorded | [0019](adr/0019-authorization-roles-and-account-ownership.md) | 4 (audit log) |
| Two tellers sending the same deposit with the same key pay it in twice | [0020](adr/0020-idempotency-keys-per-user.md) | later |
| Account history shows no counterparty or description | [0021](adr/0021-account-history-with-keyset-pages.md) | Customers module |
| There is no list of transfers: after a 500 with no money moved, "not yet" looks like "did not happen" | [0021](adr/0021-account-history-with-keyset-pages.md) | later |
| The history cursor reveals roughly how many postings the bank makes | [0021](adr/0021-account-history-with-keyset-pages.md) | later |
