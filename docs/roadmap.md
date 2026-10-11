# Roadmap

Each phase ends with working, tested software and the ADRs for the decisions it required.

| Phase | Scope | Status |
|---|---|---|
| 0 | Solution skeleton, build rules, architecture tests, local PostgreSQL, CI | Done |
| 1 | **Domain:** `Money`, `Currency`, accounts, balanced journal entries, transfer workflow; unit tests | Next |
| 2 | **Persistence and API:** EF Core, transactions and isolation, idempotent `Post` and idempotent HTTP API, optimistic concurrency, concurrent-transfer integration tests, stuck-transfer recovery | |
| 3 | **Identity:** registration, JWT access and refresh tokens, roles, account ownership authorization | |
| 4 | **Messaging:** transactional outbox, RabbitMQ, idempotent consumers, Notifications module, audit log | |
| 5 | **Operations:** Serilog, OpenTelemetry, Redis, rate limiting, daily ledger reconciliation job | |
| 6 | **Extraction:** Notifications as a separate service, deployment docs, architecture diagrams | |

Later modules (Customers/KYC, Payments) slot in after phase 3 and reuse the Ledger's posting API.
