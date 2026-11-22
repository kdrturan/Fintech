# Banking

[![CI](https://github.com/kdrturan/Fintech/actions/workflows/ci.yml/badge.svg)](https://github.com/kdrturan/Fintech/actions/workflows/ci.yml)

A digital banking and wallet backend built with .NET 10 and PostgreSQL, focused on the problems that make financial systems hard: **atomic money movement, a double-entry ledger, concurrency, and idempotency.**

> Status: phase 2 of the [roadmap](docs/roadmap.md) is done: accounts, deposits and transfers over HTTP, persisted in PostgreSQL, safe under concurrent requests, retries and crashes. Phase 3 (identity) is next.

## Architecture

A **modular monolith**: one deployable application whose modules are isolated as if they were services. Each module has its own PostgreSQL schema and talks to other modules only through a `Contracts` project.

```
                  +-------------------+
   HTTP  -------> |    Banking.Api    |  composition root
                  +---------+---------+
                            |
         +------------------+------------------+
         v                                     v
  +--------------+   Ledger.Contracts   +--------------+
  |  Transfers   | -------------------> |    Ledger    |  the only code that
  |  (workflow)  |    Post(key, ...)    | (posting     |  changes a balance
  +--------------+                      |  engine)     |
                                        +--------------+
```

The **Ledger** is the single place where money moves: it applies balanced journal entries atomically and idempotently ([ADR 0009](docs/adr/0009-double-entry-ledger-model.md)). Business modules such as **Transfers** (and later Payments) own workflows and ask the Ledger to post. See [ADR 0003](docs/adr/0003-modular-monolith-with-ledger-as-posting-engine.md) for the reasoning and the alternatives that were rejected.

Module boundaries are enforced by [architecture tests](tests/Banking.ArchitectureTests) that inspect the compiled assemblies, not by convention. HTTP endpoints are controllers in a `Presentation` project per module ([ADR 0005](docs/adr/0005-use-controllers-for-http-endpoints.md), [ADR 0013](docs/adr/0013-http-api-layer.md)).

### How a transfer stays correct

| Risk | Protection |
|---|---|
| Two transfers spend the same balance | Accounts are locked with `SELECT ... FOR UPDATE` in id order ([ADR 0011](docs/adr/0011-concurrency-control.md)) |
| A double-click or a retry after a lost response | `Idempotency-Key`: the first response is stored and replayed ([ADR 0012](docs/adr/0012-idempotency.md)) |
| A crash after the money moved | The transfer is recorded as `Pending` first; a recovery job finishes it ([ADR 0014](docs/adr/0014-synchronous-transfers-and-recovery.md)) |
| The recovery job posts the same transfer again | The Ledger's unique idempotency key returns the original result ([ADR 0012](docs/adr/0012-idempotency.md)) |
| The request and the recovery job finish the same transfer | Optimistic concurrency on `xmin`: the later write loses ([ADR 0011](docs/adr/0011-concurrency-control.md)) |

## Repository layout

```
src/
  Banking.Api/                    host and composition root
  BuildingBlocks/
    Banking.SharedKernel/         Money, Currency, Result, Error
    Banking.Web/                  problem details, Idempotency-Key filter and store
  Modules/
    Ledger/                       Domain / Application / Infrastructure / Contracts / Presentation
    Transfers/                    Domain / Application / Infrastructure / Contracts / Presentation
tests/
  Banking.UnitTests/              domain rules, no database
  Banking.ArchitectureTests/      dependency rules between modules and layers
  Banking.IntegrationTests/       the real API on a real PostgreSQL through Testcontainers
docs/
  adr/                            architecture decision records
  roadmap.md
```

## Getting started

Prerequisites: .NET SDK 10.0.203 or later (any 10.0 feature band, see `global.json`) and Docker.

```bash
dotnet tool restore                 # dotnet-ef, pinned in .config/dotnet-tools.json
docker compose up -d                # local PostgreSQL 18 on port 5432
dotnet build Banking.slnx
dotnet test --solution Banking.slnx # integration tests start their own PostgreSQL container
```

Migrations are applied as an explicit step, never at startup ([ADR 0010](docs/adr/0010-persistence-with-ef-core.md)). One command per database context:

```bash
dotnet ef database update --project src/BuildingBlocks/Banking.Web --startup-project src/Banking.Api --context IdempotencyDbContext
dotnet ef database update --project src/Modules/Ledger/Banking.Modules.Ledger.Infrastructure --startup-project src/Banking.Api --context LedgerDbContext
dotnet ef database update --project src/Modules/Transfers/Banking.Modules.Transfers.Infrastructure --startup-project src/Banking.Api --context TransfersDbContext
```

MediatR needs a license key (free Community license, [ADR 0015](docs/adr/0015-use-mediatr.md)). It never goes into the repository:

```bash
dotnet user-secrets set "MediatR:LicenseKey" "<your key>" --project src/Banking.Api
dotnet run --project src/Banking.Api
```

The API listens on `http://localhost:5104`. Liveness check: `GET /health`. The OpenAPI document is served at `/openapi/v1.json` in Development. `src/Banking.Api/Banking.Api.http` contains ready-to-send requests.

## API

| Endpoint | Does | Idempotency-Key |
|---|---|---|
| `POST /accounts` | Opens an account in a currency | |
| `GET /accounts/{id}` | Returns the account and its balance | |
| `POST /accounts/{id}/deposits` | Pays cash in | required |
| `POST /transfers` | Moves money between two customer accounts; 201 `Completed` or 422 with `transferId` | required |
| `GET /transfers/{id}` | Returns a transfer's status and failure reason | |

Errors are RFC 9457 problem details with a stable `errorCode` ([ADR 0013](docs/adr/0013-http-api-layer.md)).

## Architecture decisions

| # | Decision |
|---|---|
| [0001](docs/adr/0001-record-architecture-decisions.md) | Record architecture decisions |
| [0002](docs/adr/0002-use-postgresql.md) | Use PostgreSQL as the primary database |
| [0003](docs/adr/0003-modular-monolith-with-ledger-as-posting-engine.md) | Modular monolith with the Ledger as a posting engine |
| [0004](docs/adr/0004-build-and-test-tooling.md) | Build and test tooling |
| [0005](docs/adr/0005-use-controllers-for-http-endpoints.md) | Use controllers for HTTP endpoints |
| [0006](docs/adr/0006-money-representation-and-rounding.md) | Money: representation, rounding and where it lives |
| [0007](docs/adr/0007-results-for-business-outcomes.md) | Results for business outcomes, exceptions for bugs |
| [0008](docs/adr/0008-identifiers.md) | Identifiers: UUIDv7 and strongly typed ids |
| [0009](docs/adr/0009-double-entry-ledger-model.md) | Double-entry ledger model |
| [0010](docs/adr/0010-persistence-with-ef-core.md) | Persistence with EF Core: direct mapping, one context per module, explicit migrations |
| [0011](docs/adr/0011-concurrency-control.md) | Concurrency control: row locks for balances, optimistic concurrency for transfers |
| [0012](docs/adr/0012-idempotency.md) | Idempotency: Idempotency-Key for HTTP, a unique key in the Ledger |
| [0013](docs/adr/0013-http-api-layer.md) | HTTP API: a Presentation project per module, problem details for errors |
| [0014](docs/adr/0014-synchronous-transfers-and-recovery.md) | Synchronous transfers with a recovery job |
| [0015](docs/adr/0015-use-mediatr.md) | Use MediatR for commands and queries |
| [0016](docs/adr/0016-integration-test-database-per-class.md) | Integration tests: a database per test class, copied from a template |
