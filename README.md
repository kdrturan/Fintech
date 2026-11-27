# Banking

[![CI](https://github.com/kdrturan/Fintech/actions/workflows/ci.yml/badge.svg)](https://github.com/kdrturan/Fintech/actions/workflows/ci.yml)

A digital banking and wallet backend built with .NET 10 and PostgreSQL, focused on the problems that make financial systems hard: **atomic money movement, a double-entry ledger, concurrency, and idempotency.**

> Status: phase 3 of the [roadmap](docs/roadmap.md) is done: customers register and sign in, tellers pay cash in, and every account, transfer and history page is reachable only by its owner. Money moves safely under concurrent requests, retries and crashes (phase 2). Phase 4 (messaging) is next.

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

The **Identity** module registers users and issues the tokens: short RS256 access tokens and rotating refresh tokens ([ADR 0017](docs/adr/0017-identity-with-aspnet-core-identity-and-own-jwts.md), [ADR 0018](docs/adr/0018-access-and-refresh-tokens.md)). The other modules never call it; they read the caller's id from the token. Roles decide what a caller may do, and the Ledger checks who owns each account ([ADR 0019](docs/adr/0019-authorization-roles-and-account-ownership.md)).

Module boundaries are enforced by [architecture tests](tests/Banking.ArchitectureTests) that inspect the compiled assemblies, not by convention. HTTP endpoints are controllers in a `Presentation` project per module ([ADR 0005](docs/adr/0005-use-controllers-for-http-endpoints.md), [ADR 0013](docs/adr/0013-http-api-layer.md)).

### How a transfer stays correct

| Risk | Protection |
|---|---|
| Two transfers spend the same balance | Accounts are locked with `SELECT ... FOR UPDATE` in id order ([ADR 0011](docs/adr/0011-concurrency-control.md)) |
| A double-click or a retry after a lost response | `Idempotency-Key`: the first response is stored and replayed ([ADR 0012](docs/adr/0012-idempotency.md)) |
| Someone moves money out of another customer's account | The Ledger checks that the source belongs to the requester, on every post, the recovery job's included ([ADR 0019](docs/adr/0019-authorization-roles-and-account-ownership.md)) |
| Someone sends another customer's idempotency key | Keys belong to the user who sent them ([ADR 0020](docs/adr/0020-idempotency-keys-per-user.md)) |
| A crash after the money moved | The transfer is recorded as `Pending` first; a recovery job finishes it ([ADR 0014](docs/adr/0014-synchronous-transfers-and-recovery.md)) |
| The recovery job posts the same transfer again | The Ledger's unique idempotency key returns the original result ([ADR 0012](docs/adr/0012-idempotency.md)) |
| The request and the recovery job finish the same transfer | Optimistic concurrency on `xmin`: the later write loses ([ADR 0011](docs/adr/0011-concurrency-control.md)) |

## Repository layout

```
src/
  Banking.Api/                    host and composition root
  BuildingBlocks/
    Banking.SharedKernel/         Money, Currency, Result, Error
    Banking.Web/                  problem details, JWT validation, Idempotency-Key filter and store
  Modules/
    Identity/                     Domain / Application / Infrastructure / Contracts / Presentation
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
dotnet ef database update --project src/Modules/Identity/Banking.Modules.Identity.Infrastructure --startup-project src/Banking.Api --context IdentityModuleDbContext
dotnet ef database update --project src/Modules/Ledger/Banking.Modules.Ledger.Infrastructure --startup-project src/Banking.Api --context LedgerDbContext
dotnet ef database update --project src/Modules/Transfers/Banking.Modules.Transfers.Infrastructure --startup-project src/Banking.Api --context TransfersDbContext
```

Two secrets never go into the repository: a MediatR license key (free Community license, [ADR 0015](docs/adr/0015-use-mediatr.md)) and the RSA key that signs access tokens. The API refuses to start without the signing key. Generate one in PowerShell and store both with user-secrets. `SeedTeller` is optional: the API creates that teller when it starts, and without one nobody can pay cash in.

```powershell
$rsa = [System.Security.Cryptography.RSA]::Create(2048)
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($rsa.ExportPkcs8PrivateKey())) --project src/Banking.Api
dotnet user-secrets set "MediatR:LicenseKey" "<your key>" --project src/Banking.Api
dotnet user-secrets set "SeedTeller:Email" "teller@example.com" --project src/Banking.Api
dotnet user-secrets set "SeedTeller:Password" "<12+ characters: upper, lower, digit and symbol>" --project src/Banking.Api
dotnet run --project src/Banking.Api
```

The API listens on `http://localhost:5104`. Liveness check: `GET /health`. The OpenAPI document is served at `/openapi/v1.json` in Development. `src/Banking.Api/Banking.Api.http` contains ready-to-send requests: register, sign in, then paste the access token into `@token`.

## API

Every endpoint needs `Authorization: Bearer <access token>` except register, login and refresh.

| Endpoint | Does | Idempotency-Key |
|---|---|---|
| `POST /auth/register` | Creates a customer (e-mail and password) | |
| `POST /auth/login` | Returns an access token (15 minutes) and a refresh token (14 days) | |
| `POST /auth/refresh` | Exchanges a refresh token for a new pair; a reused one ends the session | |
| `POST /auth/logout` | Ends the session of a refresh token | |
| `GET /me` | Who the token belongs to | |
| `POST /accounts` | Opens an account in a currency (customers) | required |
| `GET /accounts` | Lists the caller's own accounts | |
| `GET /accounts/{id}` | Returns the account and its balance (its owner) | |
| `GET /accounts/{id}/entries` | What happened on the account, newest first, in pages: `limit`, `after` (its owner) | |
| `POST /accounts/{id}/deposits` | Pays cash in (tellers only) | required |
| `POST /transfers` | Moves money from the caller's own account; 201 `Completed` or 422 with `transferId` | required |
| `GET /transfers/{id}` | Returns a transfer's status and failure reason (who requested it) | |

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
| [0017](docs/adr/0017-identity-with-aspnet-core-identity-and-own-jwts.md) | Identity: ASP.NET Core Identity's core, with our own tokens |
| [0018](docs/adr/0018-access-and-refresh-tokens.md) | Tokens: short RS256 access tokens, rotating refresh tokens in sessions |
| [0019](docs/adr/0019-authorization-roles-and-account-ownership.md) | Authorization: roles for what you may do, ownership for what is yours |
| [0020](docs/adr/0020-idempotency-keys-per-user.md) | Idempotency keys belong to the user who sent them |
| [0021](docs/adr/0021-account-history-with-keyset-pages.md) | Account history: keyset pages ordered by posting id |
