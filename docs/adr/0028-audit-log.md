# ADR-0028: Audit log

- Status: Accepted
- Date: 2026-10-20

## Context

Tenant administrators can create and change roles, hand them out, invite people by e-mail, and platform
administrators can create and rename tenants (ADR-0022, ADR-0027). Each row records who last changed it
(`IAuditable`), but nothing records *what* happened over time: after a role loses a permission, or a user is
removed from a role, or an invitation is accepted by someone unexpected, there is no history to look at. Application
logs are not that history: they are operational, retained for days, shared across tenants and not readable by
tenant administrators.

## Alternatives considered

- **What produces entries:** an EF Core interceptor that records every change to every entity (complete, but it
  records column changes rather than intent, copies data such as e-mail addresses and token hashes into the log, and
  grows with every feature), or **explicit calls in the use cases that change security-relevant state**, with a
  description chosen by the use case.
- **When entries are written:** after the commit, through the outbox (ADR-0019) or a log sink (an entry can be lost
  after the change is committed), or **in the same unit of work as the change** (an entry exists exactly when the
  change does).
- **Shape:** a JSON details column per action (flexible, but needs a schema per action and parsing in every
  reader), or **fixed columns plus a bounded, human-readable description**.
- **Ownership:** a global table filtered by hand, or **tenant-owned data** (ADR-0010), so reads and writes use the
  same automatic isolation as every other feature.
- **Retention:** keep entries forever, or **delete them after a configurable retention** through housekeeping
  (ADR-0026).

## Decision

**Recording**

- `audit_log_entries` is tenant-owned. An entry has the time, the actor (the token subject from `ICurrentUser`;
  empty for commands), a stable action name (`role.created`), the id of the affected entity, and a description of
  at most 1000 characters (longer descriptions are shortened).
- Use cases record entries with `AuditLog.Record(action, targetId, description)`. Like `Outbox.Enqueue`, it only adds
  a row to the current unit of work; the use case's single `SaveChangesAsync` commits it with the change.
- Recorded actions:
  - roles: `role.created`, `role.updated` (only when the name or permissions changed), `role.deleted`,
    `role.assigned`, `role.unassigned` (not when nothing changed);
  - invitations: `invitation.created`, `invitation.revoked` (only when a pending invitation is revoked),
    `invitation.accepted`;
  - tenants: `tenant.created` (platform administration and `bootstrap`), `tenant.renamed`. They are recorded in the
    affected tenant, through `TenantOverride` (ADR-0022, ADR-0027).
- Action names are part of the API contract: never rename them.
- **Descriptions name roles, permissions and user ids, never e-mail addresses, tokens or secrets.** An invitation's
  address stays on the invitation, which is removed by housekeeping; the log keeps only the invitation id.
- Not recorded: project changes (the example feature), sign-in and other account events (not tenant-scoped, and
  already in the application log without personal data), and platform administrator grants (not tenant-scoped; the
  command prints them and `platform_administrators` shows the current state).

**Immutability**

- Entries are append-only: `AuditLogEntry` has no methods that change it, and `AppDbContext` refuses to save a
  modified or deleted entry. The only deletion is housekeeping's retention cleanup, which uses bulk deletes.

**Reading**

- `GET /api/v1/audit-log` lists the current tenant's entries, newest first, paged (ADR-0021), optionally filtered by
  `action`, `actorId` and `targetId`. It requires the new permission `audit.read`. No endpoint changes or deletes entries.
- An index on `(tenant_id, occurred_at)` serves the list.

**Retention**

- `HousekeepingCleaner` deletes entries older than `Housekeeping:AuditLogRetention` (default 365 days, between 1 and
  3650 days) in batches, across tenants, with an index on `occurred_at`.

## Consequences

- Tenant administrators can see who changed roles, assignments and invitations, and when, without access to logs.
- Every entry is committed atomically with its change; a failed request leaves no entry and no change.
- Coverage is explicit: a new security-relevant use case must call `AuditLog.Record`, and reviewers must ask for it.
  Changes made outside the use cases (SQL by an operator) are not recorded.
- Append-only is enforced by the application, not the database: someone with write access to the database can still
  change or delete entries. Deployments that need tamper evidence need database permissions or an external sink.
- Descriptions are text for people. Programs filter on the action, actor and target id, which are stable.
- Entries are removed after the retention period; longer legal retention needs a longer setting or an export.
