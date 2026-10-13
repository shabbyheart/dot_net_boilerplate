# ADR-0011: Persistence, transactions and side effects

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate committed on every repository call, had no explicit transactions, sent e-mail from
fire-and-forget tasks that used disposed request services, had no indexes or column lengths, and ran
migrations at startup in every environment. Persistence needs clear commit points, explicit schema
decisions and a safe way to perform external side effects.

## Alternatives considered

- **Commit model:** per repository call; **one commit point per use case**; explicit transactions everywhere.
- **External side effects:** call the remote service inside the request; hold a database transaction open
  around the remote call; **transactional outbox**.
- **Schema conventions:** provider defaults; **explicit decisions per column**, snake_case names.
- **Applying migrations:** at application startup; **as a separate deployment step**.

## Decision

**Provider and schema**

- EF Core with Npgsql (PostgreSQL, ADR-0009), with connection resiliency (`EnableRetryOnFailure`).
- Table and column names are snake_case (`EFCore.NamingConventions`, MIT), so SQL needs no quoting.
- Every string property needs an explicit decision: a maximum length for bounded values, or the `text`
  column type for free-form text. A model test fails on undecided string properties.
- Indexes follow real query patterns, foreign keys and uniqueness constraints, each with a short reason.
- Configuration lives in `IEntityTypeConfiguration<T>` classes in `Infrastructure/Persistence/Configurations`.
- Settings: `DatabaseOptions` (`Database:ConnectionString`), validated at startup. The connection string is a secret
  (user secrets or environment variables, never `appsettings`).

**Commits and transactions**

1. Write use cases have one clear commit point: a single `SaveChangesAsync`, which EF Core runs in a transaction.
2. Read-only use cases never call `SaveChangesAsync`; queries use `AsNoTracking()` and projections.
3. Several saves that must succeed or fail together use an explicit transaction. Because connection resiliency
   is enabled, the whole unit runs inside `Database.CreateExecutionStrategy().ExecuteAsync(...)`.
   Prefer restructuring into one save.
4. Never keep a database transaction open across a remote call (e-mail, HTTP, queue, storage).
5. Durable external side effects use the transactional outbox (M9, ADR-0019): business changes and an outbox
   message are saved together; a worker delivers at least once, so consumers must be idempotent.
6. Until the outbox exists, handlers do not call external services.

**Cross-cutting save behaviour** uses `SaveChangesInterceptor`s, one responsibility each:
`AuditTimestampsInterceptor` (ADR-0012) and `TenantStampingInterceptor` (ADR-0010).

**Migrations**

- Migrations live in `Infrastructure/Persistence/Migrations`, created with the `dotnet-ef` local tool.
  `AppDbContextDesignTimeFactory` lets the tools build the model without starting the API.
- The application never migrates its database on startup. Locally, developers run `dotnet ef database update`;
  tests apply migrations to their container; deployments apply them as a separate step (decided with A14).
- An applied migration is never edited; changes go into a new migration.

## Consequences

- Clear, testable transaction boundaries and no half-committed use cases.
- Retry-enabled connections make explicit transactions slightly more verbose (execution strategy).
- An extra package for naming conventions, in exchange for idiomatic PostgreSQL names.
