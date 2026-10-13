# ADR-0005: Data access through IAppDbContext; no repositories or Specification pattern

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate used a generic repository (Ardalis) whose every call committed immediately, so there
was no unit of work. Specifications lived in a shared project, each had to remember the soft-delete filter,
and a Mapster-based override silently changed query results. Handlers need straightforward, transactional
data access that stays testable and keeps the concrete `DbContext` out of Application.

## Alternatives considered

1. **Generic repository** (Ardalis `IRepository<T>`): duplicates what `DbSet<T>` already provides; per-call commits.
2. **One repository per aggregate**: keeps EF Core out of Application entirely; much more code per feature,
   and query shapes leak into repository method names.
3. **`IAppDbContext` in Application** exposing `DbSet<T>` and `SaveChangesAsync`, implemented by `AppDbContext`.
4. **Dapper or raw SQL**: fast and explicit, but no change tracking, filters or migrations.

## Decision

Option 3.

- `Boilerplate.Application.Common.Persistence.IAppDbContext` exposes one `DbSet<T>` per aggregate root and
  `SaveChangesAsync`. `Boilerplate.Infrastructure.Persistence.AppDbContext` implements it.
- Application may reference the `Microsoft.EntityFrameworkCore` package (added to the allowlist in
  `PackagePolicyTests`). It must not reference a database provider or `Microsoft.EntityFrameworkCore.Relational`.
- Queries are LINQ in the handler. Reused query logic becomes an `IQueryable<T>` extension only when a second
  handler needs it. No Specification pattern, no generic repository (`Ardalis.*` remains banned).
- Rules that every query must follow (tenant isolation) are global query filters, not something each handler repeats.
- Handlers are tested against real PostgreSQL (Testcontainers), not mocked `DbSet`s.

## Consequences

- One commit point per use case, because the context is the unit of work.
- Application is tied to EF Core; replacing the ORM would touch handlers.
- Untranslatable LINQ fails in integration tests, not in production.
