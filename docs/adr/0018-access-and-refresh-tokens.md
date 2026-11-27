# 0018. Tokens: short RS256 access tokens, rotating refresh tokens in sessions

- **Status:** Accepted
- **Date:** 2026-11-27

## Context

After sign-in ([ADR 0017](0017-identity-with-aspnet-core-identity-and-own-jwts.md)) every request must prove who sent it. A JWT is checked by its signature alone, without a database read, but a token that has been issued cannot be taken back. It is valid until it expires.

The intended client is a mobile app, which should keep a customer signed in for days without keeping their password.

## Decision

**Access tokens:**

- A JWT signed with **RS256**, valid for **15 minutes**. Claims: `sub` (the user id), `role`, `jti`, `iat`, `nbf`, `exp`, `iss`, `aud`. Nothing personal: anyone holding a JWT can read its payload.
- Signing needs the RSA private key. Checking needs only the public half, so a service split out later (Notifications, phase 6) can validate tokens without being able to issue them.
- **The key** is a base64 PKCS#8 RSA key of at least 2048 bits, kept in user-secrets locally (any other configuration source, such as an environment variable, works the same) and never in the repository. The integration tests generate a fresh key for each run, so CI needs no secret. The API refuses to start without it. The check runs at startup, not at registration, so `dotnet ef` works without a key.
- **Validation:**
  - Only RS256 is accepted (`ValidAlgorithms`): the same key with another RSA algorithm, such as PS256, would otherwise be accepted too.
  - Clock skew is 30 seconds instead of the default 5 minutes, which would let a 15-minute token live for 20.
  - `MapInboundClaims` is off, so `sub` and `role` keep their JWT names.
- **Secure by default:** a fallback policy requires an authenticated user on every endpoint. Only register, login, refresh, `/health` and the OpenAPI document are `[AllowAnonymous]`. It is set on each action rather than on `AuthController`, where it would silently open logout too.

**Refresh tokens:**

- 32 random bytes, Base64Url encoded, valid for **14 days**, returned in the response body. Token responses carry `Cache-Control: no-store`.
- **Only the SHA-256 hash is stored.** A leaked database holds nothing usable. A fast hash is enough here: 256 random bits cannot be guessed the way a password can.
- **A session** is one sign-in on one device, and it owns a chain of refresh tokens. Every refresh uses up the token and issues the next one (**rotation**).
- **Reuse detection** (RFC 9700): a token that was already used means two parties hold it, the customer and a thief, and nobody can tell which one is calling. The whole session is revoked and both must sign in again.
  - Revocation is stored on the session, not on each token, so a token being issued at that moment cannot escape it.
  - The refresh locks the session row (`FOR UPDATE`). Of two requests with the same token, the second waits and then finds it used.
  - The revocation is saved even though the refresh fails.
- Unknown, expired, revoked and reused tokens get the same answer (`auth.invalid_refresh_token`): a thief learns nothing from it.
- **Roles are read again on every refresh**, not copied from the old token, so a role that is given or taken away counts from the next refresh.
- **Logout** needs a valid access token and the refresh token. It revokes the session only if that token belongs to the caller, and always answers 204.

## Alternatives considered

- **HS256 (one shared secret):** simpler, but everything that can check a token can also issue one.
- **A long-lived access token and no refresh token:** one token to handle, but a stolen token would work for days and could not be stopped.
- **A refresh token that is itself a JWT:** nothing to store, but then it cannot be revoked, which is the reason it exists.
- **A grace period for reuse** (accept the previous token for a few seconds): fewer customers signed out after a lost response, but a thief racing the customer gets the same window. We chose the safe side.
- **An HttpOnly cookie for the refresh token:** right for a browser client, where script on the page must not reach it. Not needed for a mobile app.
- **A denylist of access token ids (`jti`)** so logout takes effect at once: it brings back the database read on every request that JWTs avoid.

## Consequences

- **Logout and a removed role take effect late:** an access token lives up to 15 minutes, plus 30 seconds of skew, after either.
- **A lost refresh response signs the customer out:** the app retries with the old token, which counts as reuse.
- **No absolute session lifetime:** a customer who refreshes at least every 14 days stays signed in indefinitely. Real banks usually set a limit.
- Used, expired and revoked rows are never deleted. Cleanup belongs with the other operational jobs (phase 5).
- Rotating the signing key means every issued access token stops working at once. Publishing several keys with key ids (`kid`, a JWKS endpoint) is needed before keys are rotated in production, and at the latest when a second service validates tokens (phase 6).
