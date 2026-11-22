# 0014. Synchronous transfers with a recovery job

- **Status:** Accepted
- **Date:** 2026-11-23

## Context

[ADR 0003](0003-modular-monolith-with-ledger-as-posting-engine.md) splits a transfer into three steps in separate transactions, because Transfers and the Ledger own separate schemas: record it as `Pending`, post it to the Ledger, record the outcome. This ADR decides when those steps run and how a transfer stuck after a crash is finished.

The intent is recorded first on purpose. If the process dies after the Ledger moved the money, a `Pending` transfer is the only trace that something must be finished.

## Decision

- **`POST /transfers` is synchronous:** the request runs all three steps and answers with the final status (201 `Completed`, or 422 for a transfer the Ledger rejected).
- A **recovery job** (`BackgroundService`) runs on an interval. Each round:
  - opens its own DI scope, because the hosted service is a singleton and must not hold a `DbContext`;
  - in one transaction, picks at most `BatchSize` `Pending` transfers older than `StuckAfter`, oldest first, locked with `FOR UPDATE SKIP LOCKED`;
  - posts each to the Ledger again with the same key ([ADR 0012](0012-idempotency.md)): the money moves if it had not, or the Ledger returns the original result if it had;
  - records the outcomes and commits.
- Why each part:
  - `StuckAfter` keeps the job away from transfers whose request is still running.
  - `SKIP LOCKED` lets several instances run the job without taking the same transfer.
  - The row lock makes a slow request that finishes the same transfer wait; its optimistic concurrency check then fails and it reports the job's outcome ([ADR 0011](0011-concurrency-control.md)).
- Only `Pending` transfers are retried. `Completed` and `Failed` are final.
- Settings come from the `TransferRecovery` configuration section (defaults: `Enabled` true, `Interval` 10 s, `StuckAfter` 30 s, `BatchSize` 20), so they can change per environment without a release. `TimeProvider` is injected so the clock can be replaced.

## Alternatives considered

- **Asynchronous transfers** (202 Accepted, a worker does the rest, the client polls): the model for transfers that leave the bank (EFT, FAST), where the outcome takes time. For transfers inside the bank it only adds latency and polling.
- **Hangfire or Quartz.NET for the job:** scheduling, dashboards and history out of the box, but a dependency and tables of their own for one periodic task. To be reconsidered with the reconciliation job (phase 5).
- **No recovery, let support fix stuck transfers by hand:** acceptable only if crashes are rare and someone watches for them; the money has already moved, so the customer sees a wrong status until then.

## Consequences

- A crash between the steps delays a transfer's status by up to `StuckAfter` plus `Interval`; the money is never moved twice. Together with the stored 500 ([ADR 0012](0012-idempotency.md)), a retry after such a crash creates nothing either.
- The job holds its row locks while it calls the Ledger, which is why rounds are small.
- **A round is all or nothing.** If one transfer throws, the round rolls back the status changes of the whole batch (the Ledger posts stay, and are returned again on the next round). A transfer that throws every time blocks its batch on every round. Known limitation; the fix is to process and save each transfer separately and set a repeatedly failing one aside.
- The job is tested directly (`StuckTransferRecovery.RunOnceAsync`): stuck before the post, stuck after the post, several in one round, failed transfers never retried, young transfers left alone, and a crash after the post followed by a retry with the same key (`CrashAfterPostingTests`). The background loop is switched off in tests. Nothing tests two instances at once; `SKIP LOCKED` is covered by reasoning, not by a test.
- The settings are not validated yet; a `StuckAfter` of zero would race with running requests. `ValidateOnStart` is a small follow-up.
