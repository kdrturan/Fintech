# 0017. Identity: ASP.NET Core Identity's core, with our own tokens

- **Status:** Accepted
- **Date:** 2026-11-27

## Context

Until phase 2 anyone who could reach the API could open accounts, pay cash in and move money out of any account. Phase 3 needs customers who register and sign in, and an API that knows who is calling.

Password storage, lockout and e-mail uniqueness are easy to get subtly wrong, and in a bank a mistake there is a breach. The question was how much of it to build.

## Decision

- **A new Identity module** with the same five projects as the others and its own `identity` schema ([ADR 0003](0003-modular-monolith-with-ledger-as-posting-engine.md), [ADR 0013](0013-http-api-layer.md)). Other modules reference only `Identity.Contracts`, which holds the role names. The architecture tests cover it.
- **The core of ASP.NET Core Identity** (`AddIdentityCore` with roles and the EF Core stores) for users, password hashing and lockout. No cookies (`AddIdentity`) and no ready-made endpoints (`MapIdentityApi`): the module issues its own tokens ([ADR 0018](0018-access-and-refresh-tokens.md)).
- **Endpoints:** `POST /auth/register` (201, no token: the client signs in next), `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`, `GET /me`. Registration always creates a `Customer` ([ADR 0019](0019-authorization-roles-and-account-ownership.md)).
- **Passwords:**
  - At least 12 characters, plus Identity's default rules (a digit, a lower and an upper case letter, a symbol).
  - Hashed with PBKDF2-HMAC-SHA512 at 210,000 iterations, OWASP's current figure; Identity's default is 100,000. A password stored with fewer iterations is re-hashed on its owner's next sign-in.
- **Lockout:** five wrong passwords lock the user for 15 minutes, new users included. The lock is checked before the password, so the right password is refused too while it lasts.
- **One answer for an unknown e-mail and a wrong password** (`auth.invalid_credentials`). Two answers would tell anyone which addresses have an account (user enumeration).
- **The e-mail is also the user name.** Identity checks e-mail uniqueness in code only; the user name has a unique index, so two registrations racing for one address cannot both succeed (409 `auth.email_taken`).
- `ErrorType` gains `Unauthorized`, mapped to 401. This adds a row to the table in [ADR 0013](0013-http-api-layer.md).
- **Who is calling:** the controller reads the user id (`sub`) from the token and puts it in the command (`CreateTransferCommand(RequestedBy, ...)`). Handlers never see `HttpContext`. A caller without HTTP, the recovery job, passes the user it stored.
- **KVKK:** tokens and logs carry the user id only, never the e-mail. Passwords and tokens are never logged.
- **Two-factor authentication is out of scope.** In a real Turkish bank, BDDK's regulation on information systems and electronic banking requires two-factor authentication for customer sign-in (password plus an SMS code or an approval in the mobile app).

## Alternatives considered

- **Our own user table with `PasswordHasher<T>`:** full control and the most to learn, but lockout, e-mail normalisation and racing registrations would all be ours to get right. "Why didn't you use Identity?" is the first question it invites.
- **Keycloak or another identity provider:** what many Turkish banks run, and the right answer for an enterprise. Phase 3 would become configuring a separate server and OIDC flows, and we would never see how a token is made.
- **`MapIdentityApi`:** ready-made `/login` and `/register`, but its tokens are not JWTs and refresh behaviour cannot be controlled.
- **An `ICurrentUser` service** injected into handlers: less to pass around, but the command no longer says who asked, and every handler test needs a fake.

## Consequences

- Identity brings its own tables and concepts (security stamp, concurrency stamp) that the rest of the code does not use.
- Integration tests use real tokens: `BankingClient` registers, signs in and sends the access token. A fake authentication handler would be faster but would never test token validation.
- **Known limitations:**
  - **Timing:** for an unknown e-mail no password is hashed, so the answer comes back a few milliseconds sooner. A careful attacker could tell which addresses exist. The fix is to hash a dummy password.
  - `auth.locked_out` is only ever returned for an address that exists, and `auth.email_taken` says so outright.
  - **Lockout can be abused:** anyone who knows a customer's e-mail can lock them out for 15 minutes with five wrong passwords.
  - **Concurrent wrong passwords:** Identity protects the failure count with a concurrency stamp. Some of several simultaneous wrong attempts may not be counted.
  - Rate limiting (phase 5) slows down probing and closes the gap in the failure count. It does not stop the lockout abuse; that needs a different lockout rule, such as locking per address and source together.
