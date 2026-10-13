# ADR-0009: Database provider

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate tried to support SQL Server and PostgreSQL at once. Only SQL Server had migrations,
Staging was configured for PostgreSQL, provider switches were duplicated in several places, and raw SQL was
written in one dialect. Supporting several providers multiplies migrations, column-type decisions, collation
behaviour and test matrices. The provider must be fixed before the persistence model (M3).

## Alternatives considered

1. **PostgreSQL.** Open source, no licence cost in production, small official container image,
   first-class support in EF Core (Npgsql) and Testcontainers.
2. **SQL Server.** Strong fit where an organisation already runs SQL Server or Azure SQL; larger image,
   licensed editions in production.
3. **Both, switchable by configuration.** Maximum flexibility; doubles migrations and tests (the previous approach).

## Decision

**PostgreSQL only**, major version **18** (the current stable major; 19 is in beta).

- Local development and integration tests use the official `postgres:18-alpine` image (floating patch version).
- EF Core will use the Npgsql provider (added in M3, ADR-0011).
- Timestamps use `timestamptz` with UTC values. The Npgsql legacy timestamp behaviour switch is not used.
- Text comparison semantics (case sensitivity, collations) are decided per column in M3, not assumed.

## Consequences

- One migration set, one test matrix, one SQL dialect.
- Applications that must run on SQL Server need a superseding ADR and a provider change; the persistence code
  will keep provider-specific details inside Infrastructure to keep that change contained.
- PostgreSQL text comparison is case-sensitive by default; features that need case-insensitive lookups
  (for example e-mail addresses) must say so explicitly.
