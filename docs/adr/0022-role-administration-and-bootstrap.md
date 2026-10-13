# ADR-0022: Role administration and tenant bootstrap

- Status: Accepted
- Date: 2026-10-13

## Context

ADR-0017 introduced permissions granted through tenant-scoped roles but left role administration, escalation
guards and the first administrator to this milestone. The previous boilerplate let any holder of a user-update
permission assign themselves the super-administrator role, and seeded a super administrator with a password
hard-coded in source. Nothing can be administered over HTTP until someone holds `roles.manage` in a tenant, and
there is no tenant-creation API.

## Alternatives considered

- **Administrator role:** a normal role holding every permission (must be updated whenever a feature adds one),
  or **a built-in role that implicitly grants every permission**.
- **Escalation:** trust anyone with `roles.manage`, or **allow granting and removing only permissions the caller holds**.
- **Bootstrap:** seed at application startup from configuration (writes data on every start, on every instance),
  or **a one-off command run as a deployment step**, like migrations (ADR-0011).

## Decision

**Permission catalogue**

- `PermissionCatalog.All` (Application) lists every permission. A feature that adds permission constants adds them
  to the catalogue; a test fails otherwise. Roles can only contain catalogue permissions.
- `GET /api/v1/permissions` (`roles.read`) returns the catalogue for administration clients.

**Administrator role**

- Each tenant has at most one built-in role named `Administrators` (`Role.IsAdministrator`), enforced by a partial
  unique index. It **implicitly grants every permission**, including permissions added later, and stores none.
- It cannot be renamed, changed or deleted. Its last assignment cannot be removed, so a tenant cannot lock itself out.

**Escalation guard** (403, `ForbiddenException`)

- Creating or changing a role: every permission added or removed must be held by the caller.
- Deleting a role, assigning or unassigning it: the caller must hold every permission of the role.
- Only administrators can assign or unassign the administrator role.

**Endpoints** (`roles.read` for reads, `roles.manage` for changes)

- `GET/POST /api/v1/roles`, `GET/PUT/DELETE /api/v1/roles/{id}` (PUT replaces name and permissions).
- `GET /api/v1/roles/{id}/assignments`, `PUT/DELETE /api/v1/roles/{id}/assignments/{userId}` (assignment is idempotent).
- Users are identified by the token subject (ADR-0016); there is no user table.

**Bootstrap command**

- `dotnet Boilerplate.Api.dll bootstrap --tenant-name <name> --admin-user <subject>` creates the tenant if missing,
  its administrator role if missing, and assigns the user, in one transaction. Re-running it changes nothing.
- It runs after migrations, with the same configuration as the API, and does not start the web server.
  Exit codes: 0 success, 2 invalid arguments, 1 failure.
- Tenants are identified by name for this purpose, so tenant names are unique.
- The command acts for the new tenant through `TenantOverride` (Application), a scoped holder that
  `ClaimsTenantContext` prefers over the token claim. Background work (M9) uses the same mechanism.

## Consequences

- No credentials in code or configuration; the first administrator is whichever identity-provider user the operator names.
- New features never need data migrations to keep administrators complete.
- Delegated administration is safe: a role manager cannot hand out more than they hold.
- An administrator role with no stored permissions must be interpreted by clients through `isAdministrator`.
