# 0005. Use controllers for HTTP endpoints

- **Status:** Accepted
- **Date:** 2026-10-12

## Context

Every module (Ledger, Transfers, and later Payments and Identity) will expose its own HTTP endpoints. ASP.NET Core offers two built-in programming models for this:

- **Controllers** (`[ApiController]` classes with action methods), the MVC model that has been the default for Web APIs since ASP.NET Core 1.0.
- **Minimal APIs** (`app.MapGet(...)`, route groups and endpoint filters), introduced in .NET 6 and now promoted by the default templates.

Both are fully supported and perform well enough for this system; the choice is about structure and familiarity, not capability.

The forces that matter here:

- Each module should own its endpoints, so that adding a module does not mean editing a central routing file in `Banking.Api`.
- Most existing .NET codebases in Turkish banks and fintech companies are controller-based. The project is meant to practise patterns that transfer directly to that environment.
- Endpoint behaviour that is shared across a module (authorization policy, route prefix, versioning, error handling) should be declared once per group, close to the endpoints.

## Decision

HTTP endpoints are implemented as **controllers**. Each module groups its endpoints into controller classes, with attribute routing (`[Route("api/ledger/...")]`) and `[ApiController]` for automatic model validation and `ProblemDetails` responses.

`Banking.Api` only registers MVC (`AddControllers` / `MapControllers`) and the module assemblies that contain controllers. It does not define module endpoints itself.

## Alternatives considered

- **Minimal APIs:** less ceremony, slightly better performance, native `TypedResults` and good OpenAPI metadata, and the direction Microsoft is investing in. Route groups (`MapGroup`) can give modules the same grouping that controllers give. Not chosen because the organisation that controllers provide by convention (one class per resource, attributes for routing and policies, filters) has to be built by hand with extension methods, and because controllers are what most production banking code we will work with looks like. The performance difference is irrelevant next to database round-trips in a ledger.
- **Third-party endpoint libraries (FastEndpoints, Carter):** structured and pleasant, but they add a dependency to a core concern and are less common in the market. Being able to explain the built-in model is more valuable for this project.

## Consequences

- Endpoints are organised the familiar way: one controller per resource, filters and attributes for cross-cutting behaviour.
- **Controller discovery across assemblies.** ASP.NET Core only finds controllers in the entry assembly and its application parts. Controllers that live inside module projects must be registered explicitly, for example with `AddControllers().AddApplicationPart(typeof(SomeLedgerController).Assembly)`, ideally behind a per-module extension method.
- **Framework reference in module projects.** Module projects use `Microsoft.NET.Sdk`. The project that holds a module's controllers needs `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, which brings ASP.NET Core types into a module. This must stay out of `Domain` and `Application`.
- More boilerplate per endpoint than minimal APIs (class, constructor, attributes).
- **Open question:** which project holds a module's controllers. Options are the existing `Infrastructure` project, which the dependency rules already allow `Banking.Api` to reference, or a new per-module presentation project such as `Banking.Modules.Ledger.Api`. A new layer must also be added to the architecture tests, which deliberately fail on assemblies they cannot classify. This will be decided when the first module endpoint is written in phase 1 and recorded in a new ADR.
