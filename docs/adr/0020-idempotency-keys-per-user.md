# 0020. Idempotency keys belong to the user who sent them

- **Status:** Accepted. Amends [ADR 0012](0012-idempotency.md).
- **Date:** 2026-11-27

## Context

[ADR 0012](0012-idempotency.md) made idempotency keys global: the primary key was the key alone. Once requests had users ([ADR 0017](0017-identity-with-aspnet-core-identity-and-own-jwts.md)), a test showed two holes:

- **Leak:** a customer who sent another customer's key with the same body got that customer's stored response (201 with `Idempotent-Replayed`, and the other customer's account id). No money moved, but the response was someone else's.
- **Squatting:** with a different body the same key answered 422 `key_reused`. Anyone who used a key first could make another customer's request fail.

The ownership checks ([ADR 0019](0019-authorization-roles-and-account-ownership.md)) could not help. A replay is answered by the filter, so the request never reaches the handler that would check the owner.

`POST /accounts` was also not idempotent: a retry after a lost response opened a second empty account. Its body is the same for every customer (`{"currency":"TRY"}`), so with global keys the key alone was enough to receive someone else's account.

## Decision

- The primary key of `idempotency.requests` is **`(user_id, key)`**. The user id comes from the access token only, never from the request.
- Every statement on the table includes the user id: claiming, completing and releasing a key. A statement on the key alone would also change another user's row.
- The filter runs after authorization; it is a resource filter ([ADR 0012](0012-idempotency.md)). A request without a valid token is answered 401 before any key is read or claimed, so the filter always has a user.
- **`POST /accounts` is `[Idempotent]`** and requires an `Idempotency-Key`.
- Rows stored before this change keep the empty user id. No token carries that id, so nobody can replay them.
- Everything else in ADR 0012 stands: the fingerprint, the stored first response, 400, 409 and 422, and storing an exception as a 500.

## Alternatives considered

- **Keep global keys and rely on the fingerprint:** a different body no longer replays, which closes most of the leak but none of the squatting. A key is still one row, so the first user to send it owns it. For `POST /accounts` it closes nothing, because every customer sends the same body.
- **Put the user id in the fingerprint:** the same result. The key is still one row, and the second user gets 422.
- **Keys issued by the server:** no collisions, but every attempt needs an extra round trip before it can be retried safely, which is the case idempotency keys exist for.

## Consequences

- Two customers can use the same key without noticing each other.
- **A deposit's key now belongs to the teller.** If two different tellers send the same deposit with the same key, it is paid in twice; the global key used to prevent that. One teller retrying is safe.
- **During the release:** a retry of a request that started before the migration runs again after it, because its row has the empty user id. In production, wait until earlier retries have expired before deploying.
- **Rolling back:** if two users have used the same key, the `Down` migration fails and its transaction rolls back, so no data is lost.
- **A replay has no `Location` header**, only the stored status code and body. That has been true for transfers since ADR 0012 and is now visible for accounts too.
- After a stored 500 on `POST /accounts`, a retry with a new key may open a second empty account. `GET /accounts` ([ADR 0021](0021-account-history-with-keyset-pages.md)) shows it.
- Stored keys still grow without limit until the cleanup job (phase 5).
