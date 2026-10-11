# 0004. Build and test tooling

- **Status:** Accepted
- **Date:** 2026-10-12

## Context

The codebase should stay consistent as it grows, build reproducibly on any machine and in CI, and avoid dependencies whose licensing is unsuitable for a public project.

## Decision

- **SDK pinning:** `global.json` requires .NET SDK 10.0.203 or a later 10.0 feature band (`rollForward: latestFeature`), so every machine and CI build with the same major and minor version.
- **Shared build settings:** `Directory.Build.props` enables nullable reference types, `latest-recommended` analyzers, code-style enforcement at build time and `TreatWarningsAsErrors`. Culture-sensitive string and number APIs (CA1304/1305/1307/1309/1310/1311) are errors: on a `tr-TR` machine, `"FILE".ToLower()` yields `"fıle"`, and parsing `"1.5"` as a number yields `15`.
- **Central Package Management:** every NuGet version is declared once in `Directory.Packages.props`, with transitive pinning enabled.
- **Tests:** xUnit v3 on Microsoft.Testing.Platform (the .NET 10 `dotnet test` experience), Shouldly for assertions, and Testcontainers for integration tests against a real PostgreSQL instance.
- **Architecture tests** guard the module rules from [ADR 0003](0003-modular-monolith-with-ledger-as-posting-engine.md).
- **CI:** GitHub Actions runs restore, a formatting check, build and all tests on every push and pull request.

## Alternatives considered

- **FluentAssertions:** moved to a commercial license in v8. Shouldly is MIT-licensed and sufficient.
- **In-memory database or SQLite for integration tests:** different SQL dialect, locking and isolation behaviour. Concurrency tests would prove nothing about production.
- **Warnings as suggestions:** warnings that do not fail the build accumulate until nobody reads them.

## Consequences

- Any analyzer or style violation fails the build locally and in CI.
- Test projects that run zero tests fail (Microsoft.Testing.Platform exit code 8), so a test project is only added together with its first tests.
- Docker is required locally for integration tests.
- Licensing is checked before adding a dependency. MediatR, AutoMapper and MassTransit v9 are commercial; each will be evaluated in its own ADR when needed.
