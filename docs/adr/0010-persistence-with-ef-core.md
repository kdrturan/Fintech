# 0010. Persistence with EF Core: direct mapping, one context per module, explicit migrations

- **Status:** Accepted
- **Date:** 2026-11-23

## Context

Phase 1 built the domain without a database. Phase 2 stores accounts, journal entries and transfers in PostgreSQL ([ADR 0002](0002-use-postgresql.md)), one schema per module ([ADR 0003](0003-modular-monolith-with-ledger-as-posting-engine.md)). The domain classes protect their invariants with private constructors, get-only properties and value objects (`Money`, typed ids), which an ORM must be able to read and rebuild.

## Decision

- **EF Core with Npgsql.** Each module has its own `DbContext` in its Infrastructure project, using only its own schema (`ledger`, `transfers`) and its own migrations history table. The idempotency store has a third context and schema (`idempotency`) in `Banking.Web` ([ADR 0012](0012-idempotency.md)). Table and column names are snake_case (`EFCore.NamingConventions`), so hand-written SQL needs no quoted identifiers.
- **Domain classes are mapped directly**, with `IEntityTypeConfiguration<T>` classes in Infrastructure. The domain does not reference EF Core.
  - Typed ids use value converters (`AccountId` ↔ `uuid`).
  - `Money` is a **complex type**: its amount and currency are columns of the owner's row (`balance_amount`, `balance_currency`), `numeric(19,4)` and `character(3)`.
  - Enums are stored as text, so the database stays readable.
  - Values that only the database needs are **shadow properties**, invisible to the domain: a transfer's `CreatedAt` (for the recovery job) and its row version ([ADR 0011](0011-concurrency-control.md)).
- **Concessions the domain makes to EF Core**, all private: a parameterless constructor where EF cannot pass a complex type to a constructor (`Posting`, `Transfer`), and a private list behind `JournalEntry.Postings` that EF can fill. Each one is commented in the code.
- **No foreign keys across schemas.** A transfer stores account ids as plain `uuid`s; the accounts belong to the Ledger.
- The bank's own accounts (cash, one per currency) have fixed ids and are created by a **data migration** (`SeedBankAccounts`).
- **Migrations are applied by an explicit step**, never at application start: `dotnet ef database update` in development (one command per context, see the README), a migration bundle in production, `MigrateAsync` in the test fixture.

## Alternatives considered

- **Separate persistence models** (`AccountRecord`) mapped to and from the domain: the domain stays untouched, but every entity is written twice and the mapping code has bugs of its own (a new property that is never copied is lost silently). Rebuilding an `Account` with a balance would also need a way around its "new accounts start at zero" constructor.
- **Dapper with hand-written SQL:** full control and no ORM concessions, but every query and every mapping is hand-written. The posting path and the recovery job already use raw SQL where it matters (row locks).
- **Migrating at application start:** convenient, but several instances starting together race to migrate, and the application would need permission to change the schema.

## Consequences

- One model per concept. Changing an entity means changing its configuration and adding a migration.
- Raw SQL cannot map complex-type columns that were renamed with `HasColumnName`; queries that need raw SQL (row locks) select ids only and load the entities with LINQ.
- A tracking query returns an entity the context already tracks without refreshing it (identity resolution). Code that must see the stored state detaches first (`TransferRepository.FindAsync`), refuses to run with tracked entities (`LedgerPoster`), or uses a fresh scope (the recovery job).
- Migrations are generated code and excluded from analyzers and formatting (`.editorconfig`).
