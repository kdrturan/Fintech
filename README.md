# Banking

[![CI](https://github.com/kdrturan/Fintech/actions/workflows/ci.yml/badge.svg)](https://github.com/kdrturan/Fintech/actions/workflows/ci.yml)

A digital banking and wallet backend built with .NET 10 and PostgreSQL, focused on the problems that make financial systems hard: **atomic money movement, a double-entry ledger, concurrency, and idempotency.**

> Status: phase 0 of the [roadmap](docs/roadmap.md) (foundations: solution structure, build rules, architecture tests, local PostgreSQL, CI). Domain code starts in phase 1.

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

The **Ledger** is the single place where money moves: it applies balanced journal entries atomically and idempotently. Business modules such as **Transfers** (and later Payments) own workflows and ask the Ledger to post. See [ADR 0003](docs/adr/0003-modular-monolith-with-ledger-as-posting-engine.md) for the reasoning and the alternatives that were rejected.

Module boundaries are enforced by [architecture tests](tests/Banking.ArchitectureTests) that inspect the compiled assemblies, not by convention. HTTP endpoints are written as controllers, owned by each module ([ADR 0005](docs/adr/0005-use-controllers-for-http-endpoints.md)).

## Repository layout

```
src/
  Banking.Api/                    host and composition root
  BuildingBlocks/
    Banking.SharedKernel/         shared building blocks (filled in phase 1)
  Modules/
    Ledger/                       Domain / Application / Infrastructure / Contracts
    Transfers/                    Domain / Application / Infrastructure / Contracts
tests/
  Banking.ArchitectureTests/      dependency rules between modules and layers
  Banking.IntegrationTests/       real PostgreSQL through Testcontainers
docs/
  adr/                            architecture decision records
  roadmap.md
```

## Getting started

Prerequisites: .NET SDK 10.0.203 or later (any 10.0 feature band, see `global.json`) and Docker.

```bash
docker compose up -d                # local PostgreSQL 18 on port 5432
dotnet build Banking.slnx
dotnet test --solution Banking.slnx # integration tests start their own PostgreSQL container
dotnet run --project src/Banking.Api
```

The API listens on `http://localhost:5104`. Liveness check: `GET /health`. The OpenAPI document is served at `/openapi/v1.json` in Development. `src/Banking.Api/Banking.Api.http` contains ready-to-send requests.

## Architecture decisions

| # | Decision |
|---|---|
| [0001](docs/adr/0001-record-architecture-decisions.md) | Record architecture decisions |
| [0002](docs/adr/0002-use-postgresql.md) | Use PostgreSQL as the primary database |
| [0003](docs/adr/0003-modular-monolith-with-ledger-as-posting-engine.md) | Modular monolith with the Ledger as a posting engine |
| [0004](docs/adr/0004-build-and-test-tooling.md) | Build and test tooling |
| [0005](docs/adr/0005-use-controllers-for-http-endpoints.md) | Use controllers for HTTP endpoints |
