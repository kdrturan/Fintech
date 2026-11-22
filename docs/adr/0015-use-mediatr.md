# 0015. Use MediatR for commands and queries

- **Status:** Accepted
- **Date:** 2026-11-23

## Context

Controllers need to call use cases in a module's Application layer ([ADR 0013](0013-http-api-layer.md)). Phase 1 chose MediatR because it is common in .NET codebases in the industry, on the condition that its license is checked first (MediatR became commercial in 2025).

## Decision

- Each use case is a request (`OpenAccountCommand`, `DepositCommand`, `CreateTransferCommand`, `GetTransferQuery`) with one handler in the module's Application project. Controllers depend only on `ISender`.
- **MediatR 13+ (14.2.0) under the free Community license.** As checked on 2026-10-18, the Community license is free for companies and individuals under USD 5 million gross annual revenue, non-profits, education and non-production use, and needs a registered license key. Version 12.5.0 is the last Apache-2.0 release but receives no further fixes.
- **The license key never goes into the repository.** The application reads `MediatR:LicenseKey` from configuration: user-secrets in development, an environment variable (`MediatR__LicenseKey`) or secret in CI and production.
- The host registers handlers through each module's `ApplicationAssembly`, so it does not reference Application projects.

## Alternatives considered

- **No mediator:** controllers call handler classes injected directly. Fewer abstractions and no license question, but less like the codebases this project prepares for, and no pipeline for cross-cutting behaviour.
- **Pinning MediatR 12.5.0:** free without a key, but an unmaintained dependency in a banking codebase.

## Consequences

- One more indirection: from a controller, "go to definition" leads to the request, not the handler.
- A module whose `ApplicationAssembly` is not registered fails at the first request ("No service for IRequestHandler"), not at startup.
- Pipeline behaviours (logging, validation, transactions) can be added in one place later.
- License terms can change; this ADR records the terms as checked and must be revisited when upgrading.
