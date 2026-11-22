# 0012. Idempotency: Idempotency-Key for HTTP, a unique key in the Ledger

- **Status:** Accepted
- **Date:** 2026-11-23

## Context

There are two separate ways money could move twice:

1. **HTTP:** a customer double-clicks, or the network drops the response and the app sends the request again. Without protection each request creates a new transfer with a new id.
2. **Ledger:** the recovery job ([ADR 0014](0014-synchronous-transfers-and-recovery.md)) posts a transfer again because it cannot know whether the first post succeeded before a crash.

Neither layer can cover the other. The Ledger's key is the transfer id, and every HTTP request creates a new transfer, so the Ledger sees a retried request as a new transfer. Only the client knows that two identical requests are one attempt and not two deliberate transfers.

This ADR closes the gap left open in [ADR 0009](0009-double-entry-ledger-model.md) ("posting the same entry twice applies it twice").

## Decision

**HTTP**, following Stripe and draft-ietf-httpapi-idempotency-key-header:

- POST endpoints that move money (deposits, transfers) require an `Idempotency-Key` header (attribute `[Idempotent]`). The client generates a key per attempt and reuses it for retries.
- The server stores the key, a fingerprint (SHA-256 of method, path and body) and the **first response**: status code and body, success or error. A retry with the same key gets that response again, marked with `Idempotent-Replayed: true`. A rejected transfer is replayed as rejected even if it would now succeed; a new attempt needs a new key.
- Errors, as in the draft: missing key → 400, the same key with a different body → 422, the first request still running → 409.
- Claiming a key is an atomic `INSERT ... ON CONFLICT DO NOTHING` on the key's primary key, so of two concurrent requests only one runs.
- **An exception is stored as a 500 and replayed.** The handler may have moved the money before it failed; releasing the key would let a retry create a second transfer and move the money again. The recovery job finishes the transfer that was left `Pending`. The key is released only when the action produced no result at all.
- The mechanism is a **resource filter**, the outermost MVC stage: a replay never reaches model binding, the controller or the handler. A request without a key is rejected before model validation runs.
- The keys live in their own `idempotency` schema, managed by the `Banking.Web` building block, because every module's endpoints use the same mechanism.

**Ledger:**

- Every journal entry carries an idempotency key (the transfer id) with a **unique index**. A post whose key exists returns the existing entry and changes nothing. The check runs after the account locks; the unique index is the last line of defence for concurrent posts that do not share an account.
- **Only successful posts are recorded.** The ledger holds money movements, not attempts. Failures are remembered where they belong: the transfer is `Failed` (final, never retried) and the HTTP layer replays the rejection.

## Alternatives considered

- **Releasing the key on a 5xx** so the client can retry: friendlier when nothing happened, but the filter cannot know whether money moved before the failure. This was the first implementation; a test that crashes after the Ledger posted showed the money moving twice (`CrashAfterPostingTests`).
- **Linking the key to the transfer** (store it on the transfer with a unique index; a retry finds the existing transfer and resumes it, as in Brandur Leach's "recovery points"): the client would get the real outcome instead of a replayed 500. It needs the key to travel into the Application layer and a new column per resource. Worth revisiting if clients need it.
- **Recording failed posts in the Ledger:** a stricter guarantee, but the ledger would hold rows that are not money movements and would start doing the transfer's job.
- **Optional keys:** friendlier for clients, but a money-moving request without a key cannot be retried safely.

## Consequences

- Clients must generate a key per attempt and reuse it for retries of that attempt.
- After a replayed 500 the client does not learn the transfer id. It needs another way to see the outcome (account history, phase 3).
- The fingerprint compares bytes: the same request with different whitespace or property order counts as a different request (422). Real clients resend the same bytes.
- Stored keys grow without limit. They must be kept at least 24 hours; a cleanup job is planned with the other operational jobs (phase 5).
- If the process dies while a request holds a key, the filter never records an outcome; the key stays "in progress" and retries get 409 until it expires. The transfer itself is still finished by the recovery job. Known limitation until keys expire.
- Keys are global for now; phase 3 scopes them per user.
