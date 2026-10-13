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
| 0005 | Querying through `IAppDbContext`; no repositories or Specification pattern | Not yet written | M3 |
| [0006](0006-error-handling-problem-details.md) | Error handling and the ProblemDetails contract | Accepted | M1 |
| [0007](0007-configuration-and-secrets.md) | Configuration and secrets | Accepted | M1 |
| [0008](0008-logging-and-observability.md) | Logging and observability | Accepted (tracing and metrics to be superseded before M10) | M1 |
| [0009](0009-database-provider.md) | Database provider (PostgreSQL) | Accepted | M2 |
| 0010 | Tenancy model | Not yet written | M3 |
| 0011 | Persistence, transactions and side effects | Not yet written | M3 |
| 0012 | IDs, time and soft delete | Not yet written | M3 |
| [0013](0013-openapi-document-and-reference-ui.md) | OpenAPI document and API reference UI | Accepted | M1 |
| 0014 | Validation | Not yet written | M4 |
| 0015 | Mapping | Not yet written | M4 |
| 0016 | Authentication strategy | Not yet written | M5 |
| 0017 | Authorization and revocation semantics | Not yet written | M6 |
| [0018](0018-containers-and-ci.md) | Containers and CI | Accepted (CD open until A14) | M2 |
| 0019 | Outbox and background processing | Not yet written | M9 |
| 0020 | API style and versioning | Not yet written | M4 |

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
