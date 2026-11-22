# 0013. HTTP API: a Presentation project per module, problem details for errors

- **Status:** Accepted
- **Date:** 2026-11-23

## Context

Phase 2 adds the first endpoints. [ADR 0005](0005-use-controllers-for-http-endpoints.md) chose controllers and left one question open: which project holds a module's controllers. Controllers must call a module's Application layer, but the composition root (`Banking.Api`) may only reference Infrastructure and Contracts ([ADR 0003](0003-modular-monolith-with-ledger-as-posting-engine.md)). Errors are `Result` failures with a stable code ([ADR 0007](0007-results-for-business-outcomes.md)), which clients must receive in a consistent shape.

## Decision

- **This answers the open question in ADR 0005:** each module gets a fifth project, **`Presentation`**, with controllers and request types only. It may reference its own Application, the Shared Kernel and `Banking.Web`; never Infrastructure (no database access) and never Domain. It is a class library with `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. The host adds each Presentation assembly with `AddApplicationPart`. The architecture tests know the new layer and reject a controller that uses Domain types, which the compiler would allow through transitive references.
- Controllers talk to the module only through MediatR requests ([ADR 0015](0015-use-mediatr.md)).
- **`Banking.Web`** is a building block for HTTP concerns shared by all modules: error responses and idempotency ([ADR 0012](0012-idempotency.md)). It depends only on the Shared Kernel.
- **`Error` carries an `ErrorType`**: `BusinessRule` (default), `Validation`, `NotFound`, `Conflict`. The domain decides the kind; the HTTP layer maps it without knowing individual codes:

  | ErrorType | Status |
  |---|---|
  | Validation | 400 |
  | NotFound | 404 |
  | Conflict | 409 |
  | BusinessRule | 422 |

- Every error response is **RFC 9457 problem details** with `errorCode` (the stable `Error.Code`) and `traceId`. Clients branch on `errorCode`, never on the text. Unexpected exceptions become a 500 problem without internal details (`UseExceptionHandler`).
- Request properties are nullable and `[Required]`, so a missing field is a 400 from model validation (with `errors`) instead of a silent default such as `0` or `Guid.Empty`. A value the handler rejects is a 400 or 422 with `errorCode`.
- A transfer the Ledger rejects is kept as `Failed` and answered with 422, with the `transferId` in the problem, so the attempt can be looked up.

## Alternatives considered

- **Controllers in Infrastructure:** no new project, but the project holding EF Core also holds HTTP, and a controller could use the `DbContext` directly and skip the Application layer.
- **Controllers in `Banking.Api`:** one place for all endpoints, but every module change touches the host, against the goal in ADR 0005 that modules own their endpoints.
- **Mapping each error code to a status in a table:** precise, but every new code needs an HTTP change, and the table drifts from the domain.
- **All business errors as 400:** simple, but "your request is malformed" and "your request is fine but the rule says no" become indistinguishable.

## Consequences

- Five projects per module. In return, "a controller cannot touch the database" is enforced by the compiler and the architecture tests.
- `ErrorType` is part of the Shared Kernel's `Error`; existing errors default to `BusinessRule`.
- A client sees two shapes of 400: `errors` from model validation, `errorCode` from the handler. Both are problem details.
