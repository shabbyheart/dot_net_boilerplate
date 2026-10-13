# AGENTS.md

Instructions for anyone changing this repository, human or AI agent. Read this file, then
[docs/architecture.md](docs/architecture.md) and the accepted ADRs in [docs/adr](docs/adr/README.md).

## Commands

Run from the repository root. Work is not done until all of these pass.

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build -c Release
dotnet test -c Release
```

- Warnings are errors, locally and in CI.
- `dotnet test` uses Microsoft.Testing.Platform (opted in via `global.json`). Run one test project with
  `dotnet test --project tests/Boilerplate.ArchitectureTests`.
- Report what you ran and the observed result. Never claim a command passed without running it.

## Project map

| Path | Purpose |
|---|---|
| `src/Boilerplate.Domain` | Entities, domain rules, domain enums. No project or package references. |
| `src/Boilerplate.Application` | Use cases (`Features/<Area>/<UseCase>/`), ports, application exceptions, settings classes. |
| `src/Boilerplate.Infrastructure` | EF Core, migrations, implementations of Application ports. |
| `src/Boilerplate.Api` | Composition root, endpoints, HTTP middleware, OpenAPI. |
| `tests/Boilerplate.UnitTests` | Domain and Application logic. |
| `tests/Boilerplate.IntegrationTests` | The real API in memory (`WebApplicationFactory`); real database from M3. |
| `tests/Boilerplate.ArchitectureTests` | Dependency and package rules. |
| `docs/` | Architecture overview and ADRs. |

## Dependency rules

The rules in [docs/architecture.md](docs/architecture.md) are enforced by `Boilerplate.ArchitectureTests`.

- If an architecture test fails, fix the code, never the test. A rule changes only through a superseding ADR,
  and then the test changes in the same pull request.
- To decide where code belongs: if it decides **what** happens, it belongs in Application or Domain. If it
  decides **how**, with a specific technology, it belongs in Infrastructure. If it is still unclear, stop and ask.

## Packages

- Versions live only in `Directory.Packages.props`.
- A new package needs a reason recorded in an ADR: purpose, licence, alternatives (including "no package").
- Banned: `MediatR` (ADR-0004) and `Ardalis.*` (ADR-0002). Prefer the .NET base library and ASP.NET Core built-ins.

## Design rules

- SOLID is about clear responsibilities and localised change, not about the number of interfaces or rules.
- Add an interface only for an external boundary (I/O, vendor, clock), a test seam the real thing cannot
  provide, or a second implementation that exists today. Never for theoretical flexibility.
- Register services explicitly (ADR-0004). No assembly scanning, no marker interfaces.
- Use cases are plain handler classes with `HandleAsync(request, cancellationToken)`.
- Write queries in the handler. Extract an `IQueryable<T>` extension only when a second handler needs the same logic.
- Write mapping by hand (`Select` projections or small mapping methods). No mapping libraries.
- Write use cases have one clear commit point; read-only handlers never save.
- Typed exceptions are the default for failures that end a request (not found, conflict, forbidden, validation).
  Do not use exceptions for ordinary control flow or expected outcomes; return an explicit value instead.
- Aim for a dependency-free Domain. Adding a package to Domain is an architectural decision that needs an ADR.

## Coding conventions

- File-scoped namespaces; one public type per file, named after the type.
- Seal classes by default when nothing should inherit; allow inheritance when it has a clear purpose.
- `record` for requests, responses and other immutable data.
- Primary constructors for dependency injection.
- Async all the way: take a `CancellationToken` as the last parameter and pass it down. No `.Result`,
  `.Wait()` or `Task.Run` in request code.
- Use `TimeProvider`, never `DateTime.UtcNow` or `DateTimeOffset.UtcNow` directly.
- No static mutable state; no static logging or error helpers. Use `ILogger<T>`.
- Logging: message templates (`"Created role {RoleId}"`), never interpolated strings. Never log passwords,
  tokens, one-time codes, request bodies or authorization headers.
- Errors: throw the typed exceptions from `Boilerplate.Application.Common.Exceptions`; `ApiExceptionHandler`
  maps them to ProblemDetails (ADR-0006). A new exception type needs a mapping and a test in the same change.
- Configuration: bind settings with the options pattern and `ValidateOnStart()`; never read `IConfiguration`
  outside the composition root. Never commit secrets (ADR-0007).
- Comments explain why, not what.

## Persistence (applies from M3)

- Every string property needs an explicit decision: a sensible maximum length for bounded values, or a
  documented unbounded type for free-form text.
- Add indexes for real query patterns, foreign keys and uniqueness constraints, with a short reason in the
  configuration. No blanket indexing.
- Never edit a migration that has been applied anywhere; add a new one.

## Tests

- New endpoints: integration tests for success, validation failure, not found or conflict where relevant,
  and authorization denial.
- Validators and domain methods with logic: unit tests.
- Security-relevant fixes (authentication, authorization, tenancy): a regression test that fails before the fix.
- Integration tests use `ApiFactory` (`tests/Boilerplate.IntegrationTests/Support`) and pass
  `TestContext.Current.CancellationToken` to async calls.
- Tests must not depend on execution order, wall-clock time or external networks (Testcontainers excepted).
- Test method names: `Method_or_behaviour_condition_expected_result` with underscores (allowed in `tests/` only).

## Documentation

- Update `README.md`, `docs/architecture.md` or feature guides when a convention or command changes.
- Accepted ADRs are immutable; supersede them with a new ADR. Update the index in `docs/adr/README.md`.

## Changing existing code

1. Read the relevant tests and ADRs first.
2. Before refactoring untested code, add a test that captures its current behaviour.
3. Keep changes scoped: no reformatting, renaming or tidying of unrelated code in the same change.
4. Do not change public contracts (routes, request and response shapes, ProblemDetails `type` values)
   without an explicit versioning decision.
5. Do not weaken validation, authorization or tests to make a build pass.
6. Implement only the milestone or task that was approved; stop and ask before starting the next one.
