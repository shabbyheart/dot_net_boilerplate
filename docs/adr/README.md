# Architecture Decision Records

One file per decision, numbered in order. Each ADR records the problem, the alternatives,
the decision, the reasoning and the trade-offs.

## Rules

- An ADR is written or finalised immediately **before** the milestone that depends on it.
- Once **Accepted**, an ADR is immutable. To change a decision, write a new ADR that
  supersedes it and mark the old one **Superseded by ADR-NNNN**.
- Code and tests must agree with accepted ADRs. If they don't, fix the code or write a superseding ADR.

## Index

| ADR | Title | Status | Needed before |
|---|---|---|---|
| [0001](0001-architecture-style.md) | Architecture style and project dependencies | Accepted | M0 |
| [0002](0002-platform-and-package-policy.md) | Platform, SDK pinning, Central Package Management, package policy | Accepted | M0 |
| [0003](0003-testing-approach.md) | Testing approach and tooling | Accepted | M0 |
| [0004](0004-explicit-di-no-mediator.md) | Explicit dependency injection; no mediator library | Accepted | M0 |
| [0005](0005-data-access-through-iappdbcontext.md) | Data access through `IAppDbContext`; no repositories or Specification pattern | Accepted | M3 |
| [0006](0006-error-handling-problem-details.md) | Error handling and the ProblemDetails contract | Accepted | M1 |
| [0007](0007-configuration-and-secrets.md) | Configuration and secrets | Accepted | M1 |
| [0008](0008-logging-and-observability.md) | Logging and observability | Accepted (tracing and metrics superseded by 0023) | M1 |
| [0009](0009-database-provider.md) | Database provider (PostgreSQL) | Accepted | M2 |
| [0010](0010-tenancy-model.md) | Tenancy model (always multi-tenant, shared schema) | Accepted | M3 |
| [0011](0011-persistence-transactions-and-side-effects.md) | Persistence, transactions and side effects | Accepted | M3 |
| [0012](0012-ids-time-and-soft-delete.md) | IDs, time and soft delete | Accepted | M3 |
| [0013](0013-openapi-document-and-reference-ui.md) | OpenAPI document and API reference UI | Accepted | M1 |
| [0014](0014-validation.md) | Validation (FluentValidation through an MVC action filter) | Accepted | M4 |
| [0015](0015-mapping.md) | Mapping (hand-written projections) | Accepted | M4 |
| [0016](0016-authentication.md) | Authentication (JWT bearer validation; issuer chosen before deployment) | Accepted | M5 |
| [0017](0017-authorization-and-revocation.md) | Authorization and revocation (permissions through roles, no cache) | Accepted | M6 |
| [0018](0018-containers-and-ci.md) | Containers and CI | Accepted (CD open until A14) | M2 |
| [0019](0019-outbox-and-email.md) | Outbox, background dispatch and e-mail | Accepted (retention: 0026) | M9 |
| [0020](0020-api-style-and-versioning.md) | API style (controllers) and versioning (`/api/v1`) | Accepted | M5 |
| [0021](0021-pagination.md) | Pagination of list endpoints | Accepted | M4 |
| [0022](0022-role-administration-and-bootstrap.md) | Role administration, administrator role and tenant bootstrap | Accepted | M7 |
| [0023](0023-tracing-and-metrics.md) | Tracing and metrics (OpenTelemetry, OTLP) | Accepted | M10 |
| [0024](0024-http-hardening.md) | HTTP hardening and operational behaviour | Accepted (assumes TLS at a proxy) | M10 |
| [0025](0025-local-accounts.md) | Local accounts (Identity, API-issued JWTs, rotating refresh tokens) | Accepted (optional, off by default; token cleanup: 0026; two-factor: 0031) | M8 |
| [0026](0026-data-retention-housekeeping.md) | Data retention and housekeeping (timed cleanup of expired tokens and delivered outbox rows) | Accepted | M11 |
| [0027](0027-platform-administration-and-invitations.md) | Platform administration (tenants over HTTP) and invitations to tenant roles | Accepted | M12 |
| [0028](0028-audit-log.md) | Audit log (append-only, tenant-owned, recorded in the use case's unit of work) | Accepted | M13 |
| [0029](0029-tenant-members.md) | Tenant members (derived from role assignments; remove and leave) | Accepted | M14 |
| [0030](0030-file-storage.md) | File storage (storage port, local disk, metadata in PostgreSQL, deletion through the outbox) | Accepted | M15 |
| [0031](0031-two-factor-sign-in.md) | Two-factor sign-in for local accounts (TOTP, hashed recovery codes, one-step login) | Accepted | M16 |

## Template

```markdown
# ADR-NNNN: Title

- Status: Proposed | Accepted | Superseded by ADR-NNNN
- Date: YYYY-MM-DD

## Context
## Alternatives considered
## Decision
## Consequences (benefits and trade-offs)
```
