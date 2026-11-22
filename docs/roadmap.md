# Roadmap

Each phase ends with working, tested software and the ADRs for the decisions it required.

| Phase | Scope | Status |
|---|---|---|
| 0 | Solution skeleton, build rules, architecture tests, local PostgreSQL, CI | Done |
| 1 | **Domain:** `Money`, `Currency`, accounts, balanced journal entries, transfer workflow; unit tests | Done |
| 2 | **Persistence and API:** EF Core, transactions and isolation, idempotent `Post` and idempotent HTTP API, optimistic concurrency, concurrent-transfer integration tests, stuck-transfer recovery | Done |
| 3 | **Identity:** registration, JWT access and refresh tokens, roles, account ownership authorization | Next |
| 4 | **Messaging:** transactional outbox, RabbitMQ, idempotent consumers, Notifications module, audit log | |
| 5 | **Operations:** Serilog, OpenTelemetry, Redis, rate limiting, daily ledger reconciliation job | |
| 6 | **Extraction:** Notifications as a separate service, deployment docs, architecture diagrams | |

Later modules (Customers/KYC, Payments) slot in after phase 3 and reuse the Ledger's posting API.

## Phase 2 in numbers

Five endpoints, three database schemas, 158 tests (98 unit, 14 architecture, 46 integration). ADRs [0010](adr/0010-persistence-with-ef-core.md) to [0016](adr/0016-integration-test-database-per-class.md).

## Carried forward

Known limitations from phase 2, each recorded in its ADR, and the phase that picks it up:

| Limitation | ADR | Phase |
|---|---|---|
| Idempotency keys never expire; a key held by a process that died answers 409 until it is cleaned up | [0012](adr/0012-idempotency.md) | 5 (cleanup job) |
| Idempotency keys are global, not per user | [0012](adr/0012-idempotency.md) | 3 |
| After a replayed 500 the client cannot see the transfer's outcome | [0012](adr/0012-idempotency.md) | 3 (account history) |
| One transfer that always throws blocks its recovery batch | [0014](adr/0014-synchronous-transfers-and-recovery.md) | 5 |
| Recovery settings are not validated at startup | [0014](adr/0014-synchronous-transfers-and-recovery.md) | small follow-up |
| `POST /accounts` is not idempotent; a retry opens a second empty account | [0012](adr/0012-idempotency.md) | 3 |
| Stored balances are not yet reconciled against the postings | [0009](adr/0009-double-entry-ledger-model.md) | 5 (reconciliation job) |
