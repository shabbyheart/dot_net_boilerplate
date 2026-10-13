# Architecture

A modular monolith with four source projects and vertical feature slices. The dependency
rules below are enforced by `tests/Boilerplate.ArchitectureTests`; the reasoning is in
[ADR-0001](adr/0001-architecture-style.md).

## Projects

| Project | Owns | Must not contain |
|---|---|---|
| `Boilerplate.Domain` | Entities with behaviour (factory methods, private setters, invariants), domain enums, domain exceptions | EF Core, ASP.NET Core, logging, serialization attributes, template names, HTTP error vocabulary |
| `Boilerplate.Application` | Use cases in `Features/<Area>/<UseCase>/` (request, handler, validator, response), ports (interfaces for external boundaries), application exceptions, settings classes | ASP.NET Core types (`HttpContext`, `IFormFile`), vendor SDKs, the `DbContext` implementation |
| `Boilerplate.Infrastructure` | EF Core `DbContext`, configurations, migrations, implementations of Application ports (email, tokens, storage, ...) | Business rules: anything that decides *what* happens belongs in Application |
| `Boilerplate.Api` | `Program.cs` (composition root), endpoints, HTTP middleware and filters, ProblemDetails mapping, OpenAPI | Business logic; direct use of Infrastructure outside `Program.cs` and `Boilerplate.Api.Registration` |

## Allowed dependencies

```mermaid
flowchart BT
  Domain
  Application --> Domain
  Infrastructure --> Application
  Api --> Application
  Api --> Infrastructure
```

1. `Domain` references no project and no package.
2. `Application` references only `Domain`, plus allowlisted packages (see `PackagePolicyTests`).
3. `Infrastructure` references only `Application`. Domain types reach it transitively; do not add a direct reference.
4. `Api` references `Application` and `Infrastructure`. Infrastructure types may be used only in `Program.cs`
   and the `Boilerplate.Api.Registration` namespace (the composition root).
5. Nothing references `Api` except tests.
6. Domain and Application never use `Microsoft.AspNetCore.*`; Domain never uses EF Core.

## How the rules are enforced

- `ProjectReferenceTests`: declared `<ProjectReference>` items match the graph above exactly, and every
  source project has a rule.
- `PackagePolicyTests`: Domain has no packages, Application packages are allowlisted, banned packages are absent.
- `LayerDependencyTests`: compiled types do not use forbidden namespaces (read from assembly metadata with NetArchTest).

Declared references and compiled usage are checked separately because the compiler drops a reference
that no code uses yet. A forbidden reference must fail the build before anyone writes code against it.

## Request flow (target, built up from M1 onwards)

```
HTTP -> exception handler -> authentication -> authorization -> endpoint
     -> validation filter -> handler (Application) -> ports / IAppDbContext -> one commit point
     -> response
Failures -> typed exceptions -> ProblemDetails (RFC 9457) with traceId
```

### Current pipeline (M1), in `src/Boilerplate.Api/Program.cs`

| Order | Component | Why here |
|---|---|---|
| 1 | `UseSerilogRequestLogging` | Outermost, so the request summary records the final status code, including handled errors |
| 2 | `UseExceptionHandler` + `ApiExceptionHandler` | Catches exceptions from everything after it and writes ProblemDetails (ADR-0006) |
| 3 | `UseStatusCodePages` | Gives body-less error responses (for example unknown routes) a ProblemDetails body |
| 4 | OpenAPI + Scalar (Development only) | Documentation endpoints (ADR-0013) |
| 5 | `/health/live` | Liveness probe; runs no dependency checks |

The service provider validates registrations at startup (`ValidateOnBuild`, `ValidateScopes`), and a failed
startup is logged and exits with code 1 (ADR-0007).
