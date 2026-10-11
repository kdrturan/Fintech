# 0003. Modular monolith with the Ledger as a posting engine

- **Status:** Accepted
- **Date:** 2026-10-12

## Context

The system covers accounts, a double-entry ledger, transfers, payments, identity, customers and notifications. Two questions shape everything else:

1. **Deployment:** one deployable unit or many services?
2. **Boundaries:** which concepts must change *atomically* and therefore belong together?

The critical invariant is: *an account's balance always equals the sum of its ledger postings, and every journal entry balances (total debits = total credits).* Anything that can break this invariant must live inside a single transactional boundary.

## Decision

### Deployment: modular monolith

One deployable application and one PostgreSQL database. Inside it, modules are isolated as if they were services:

- Each module owns a **dedicated PostgreSQL schema** (`ledger`, `transfers`, ...). No module reads or writes another module's tables, and there are no cross-schema foreign keys.
- A module is split into **four projects**: `Domain`, `Application`, `Infrastructure`, `Contracts`.
- Other modules may reference **only `Contracts`**: public interfaces and integration events built from primitive types.
- These rules are enforced by `tests/Banking.ArchitectureTests`, which fail the build on any forbidden dependency.

Allowed dependencies:

| From \ To | SharedKernel | own Domain | own Application | any Contracts |
|---|---|---|---|---|
| Domain | yes | - | no | no |
| Application | yes | yes | - | yes |
| Infrastructure | yes | yes | yes | yes |
| Contracts | no | no | no | no |
| Host (`Banking.Api`) | yes | no | no | yes (plus every module's Infrastructure) |

### Boundaries: Ledger is the posting engine; business flows are separate modules

- **Ledger** owns accounts, balances and journal entries. It exposes a single money-moving operation, conceptually `Post(idempotencyKey, postings)`, which validates and applies a balanced journal entry **in one database transaction**. It is the only code in the system allowed to change a balance.
- **Transfers** (and later **Payments**) own business workflows: validation, limits, status and history. They never change balances directly; they ask the Ledger to post.

A transfer therefore runs as:

1. Transfers records the transfer as `Pending`.
2. Transfers calls `Ledger.Post(key: transferId, ...)`. Money moves atomically.
3. Transfers marks the transfer `Completed` (or `Failed`).

If the process crashes between steps 2 and 3, the money has moved but the transfer still shows `Pending`. This is resolved by **idempotent posting**: a recovery job re-drives pending transfers and calls `Post` again with the same key. The Ledger recognises the key and returns the original result instead of posting twice.

## Alternatives considered

- **Microservices from day one:** turns local transactions into distributed ones and adds network failure modes, service discovery and deployment overhead before the domain is understood. One module (Notifications) will be extracted later to demonstrate the path.
- **Account, Ledger and Transfer as three separate modules:** a transfer would change balances and ledger entries in different transactions. A crash midway loses or duplicates money unless a saga with compensating actions is built, which means distributed-system complexity inside a single database. Keeping them in one transaction instead would silently break the module boundary.
- **One module containing Account, Ledger and Transfer:** simplest, and fully atomic. Rejected because Payments must also move money; the posting logic would either be duplicated or Payments would reach into Transfers. Workflow concerns (limits, approvals, scheduling) would also mix with ledger invariants.

## Consequences

- The ledger invariant is protected in exactly one place, by one database transaction.
- Transfers and Payments stay small and focused on workflow.
- Consistency between a transfer's *status* and the ledger is **eventual**; consistency of *money* is always immediate. This requires idempotent posting and a recovery job for stuck transfers.
- Each new module costs four projects plus a schema. We accept this in exchange for compiler- and test-enforced boundaries.
