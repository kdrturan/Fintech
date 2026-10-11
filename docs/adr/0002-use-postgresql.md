# 0002. Use PostgreSQL as the primary database

- **Status:** Accepted
- **Date:** 2026-10-12

## Context

The system moves money. The database must provide ACID transactions, well-defined isolation levels, and explicit row locking so that concurrent transfers cannot corrupt balances. It must also run identically on a laptop, in CI and in integration tests.

## Decision

Use PostgreSQL 18, accessed from .NET through Npgsql (and later EF Core). Each module owns a dedicated PostgreSQL schema (see [ADR 0003](0003-modular-monolith-with-ledger-as-posting-engine.md)).

Local development runs PostgreSQL through `docker-compose.yml`; integration tests start a throwaway instance of the same major version through Testcontainers.

## Alternatives considered

- **SQL Server:** widely used in Turkish banks and a strong option. Rejected for this project because the Linux container image is heavier and licensing is less friendly for a public portfolio project. The concepts practised here (isolation levels, row locks, optimistic concurrency) transfer directly.
- **Document or key-value stores:** no multi-row ACID transactions of the kind a double-entry ledger needs.

## Consequences

- PostgreSQL features are available when useful: `SELECT ... FOR UPDATE`, `SERIALIZABLE` isolation, the `xmin` system column for optimistic concurrency, and schemas for module isolation.
- Docker is required to run integration tests locally.
