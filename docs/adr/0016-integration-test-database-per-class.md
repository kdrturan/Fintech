# 0016. Integration tests: a database per test class, copied from a template

- **Status:** Accepted
- **Date:** 2026-11-23

## Context

Integration tests run against a real PostgreSQL in a Testcontainers container ([ADR 0002](0002-use-postgresql.md), [ADR 0004](0004-build-and-test-tooling.md)). With one shared database, tests can see each other's data. Most tests can avoid that by creating their own accounts, but some cannot: the recovery job processes *every* stuck transfer in the database ([ADR 0014](0014-synchronous-transfers-and-recovery.md)), so it would finish transfers that a test running in parallel still expects to be pending.

## Decision

- One container for the whole test assembly (an xUnit assembly fixture).
- Migrations of all three contexts (idempotency, ledger, transfers) run **once**, into a template database.
- Every test class gets **its own database**, created with `CREATE DATABASE ... TEMPLATE` (a class fixture, `BankingApiFactory`, that also hosts the API in memory). Classes run in parallel; tests within a class run one after another and use their own accounts.
- Measured on PostgreSQL 18: copying the template takes about 100–180 ms, paid once per class, not per test.
- Before copying, the fixture clears the connection pool: PostgreSQL refuses to copy a database that has open connections.
- Background work that would make tests unpredictable is switched off in the factory (`TransferRecovery:Enabled=false`); tests run a recovery round themselves.

## Alternatives considered

- **Every test creates its own data in one shared database:** no setup cost and the most common approach for simple suites, but it fails for code that scans the whole database, such as the recovery job.
- **Respawn** (empty the tables between tests): the most common choice in .NET, but tests that share a database must then run one at a time.
- **A database per test:** complete isolation, but the copy cost is paid hundreds of times.

## Consequences

- Test classes are independent of each other and of execution order.
- Tests inside a class still share data; a failing test can leave pending transfers that a later test in the same class sees.
- The container allows 300 connections, because parallel classes each have their own connection pool and the concurrency tests open many connections at once.
