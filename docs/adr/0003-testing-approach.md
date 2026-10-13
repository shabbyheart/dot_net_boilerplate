# ADR-0003: Testing approach and tooling

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate had no automated tests. Most of its defects sat at boundaries: EF Core query
translation, middleware order, authorization, configuration. Mocks would have hidden those defects.
The architecture rules in ADR-0001 also need automatic enforcement.

## Alternatives considered

- **Test framework:** NUnit, MSTest, **xUnit v3**.
- **Runner:** VSTest, or **Microsoft.Testing.Platform (MTP)**. The .NET 10 SDK's `dotnet test` requires
  an explicit opt-in to MTP, and current xUnit v3 releases run on MTP.
- **Assertions:** FluentAssertions (commercial licence from v8), plain `Assert`, **Shouldly**.
- **Architecture tests:** ArchUnitNET, **NetArchTest**, hand-written reflection.
- **Database in tests:** EF InMemory or SQLite substitutes, or **the real provider in Testcontainers**.

## Decision

- xUnit v3 on Microsoft.Testing.Platform, opted in through `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`).
- Shouldly for assertions.
- `Boilerplate.ArchitectureTests` uses two kinds of checks:
  - MSBuild files are parsed for declared project and package references. The compiler drops unused
    references from assemblies, so this is the only reliable way to catch a forbidden reference before code uses it.
  - NetArchTest checks type-level dependencies. Assemblies are read as files (`Types.FromFile`), metadata
    only, so no code from the layers under test is loaded or run.
- Integration-first from M1: `WebApplicationFactory<Program>`; from M3 also Testcontainers with the real
  database provider and Respawn to reset data between tests.
- Unit tests for domain methods, validators and pure services. Time-dependent logic uses `TimeProvider`
  and `FakeTimeProvider`.
- Shared test settings live in `tests/Directory.Build.props`.

## Consequences

- Integration tests need Docker from M3 onwards, locally and in CI.
- MTP changes some `dotnet test` options compared with VSTest (for example filters). Use the commands in `AGENTS.md`.
- Architecture rules are executable, so they stay in sync with the documentation.
