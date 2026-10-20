# ADR-0029: Tenant members

- Status: Accepted
- Date: 2026-10-20

## Context

People join a tenant through role assignments: by invitation (ADR-0027), by `bootstrap`, or by being assigned a role
(ADR-0022). Sign-in and refresh already treat "holds any role in the tenant" as membership (ADR-0025). But there is
no way to see who belongs to a tenant without listing every role's assignments, no way to remove someone from a
tenant in one step, and no way for a user to leave a tenant themselves.

## Alternatives considered

- **Storing membership:** a `tenant_members` table with its own lifecycle (a second source of truth that must stay
  consistent with role assignments, and a migration of existing data), or **membership derived from role
  assignments**, as sign-in already does.
- **Permissions:** new `members.read` / `members.manage` permissions, or **the existing `roles.read` /
  `roles.manage`**: a member is nothing but their role assignments, and removing one is unassigning roles.
- **Removing someone who holds roles the caller could not hand out:** allow it (a role manager could remove an
  administrator), or **apply the escalation guard to every role the member holds** (ADR-0022).
- **Audit entries:** new `member.*` actions with a user id as target (the target is a GUID today), or **one
  `role.unassigned` entry per role**, with a description that says why (ADR-0028).

## Decision

- A **member** of a tenant is a user (token subject) who holds at least one role in it. No new table.
- `GET /api/v1/members` (`roles.read`): the tenant's members, ordered by user id, paged (ADR-0021), each with the
  roles they hold and when they first received one. `GET /api/v1/members/{userId}`: one member; 404 for a user
  without roles in the tenant.
- `DELETE /api/v1/members/{userId}` (`roles.manage`): removes every role assignment of the user in the current tenant,
  in one transaction. The caller must be able to hand out each of those roles (escalation guard; 403 otherwise).
- `DELETE /api/v1/me/membership` (any signed-in user with a tenant in the token): the caller leaves the current tenant;
  all their role assignments in it are removed. No permission is needed: giving up one's own roles cannot escalate.
- Neither may remove the tenant's last administrator (409), as with unassigning (ADR-0022).
- Each removed assignment is recorded as `role.unassigned`, with the role as target and a description naming the user
  and whether they were removed or left (ADR-0028).
- Effects follow from existing rules: permissions stop applying on the next request (ADR-0017); a local-account
  session for the tenant ends at its next refresh (ADR-0025). Pending invitations are not touched; they are not bound
  to a user (ADR-0027).

## Consequences

- Administrators get a member view and a one-step removal without a new data model or new permissions.
- A member list costs a grouped query over role assignments, served by the existing `(tenant_id, user_id, role_id)`
  index, plus one query for the roles of the users on the page.
- Members have no profile (name, e-mail): users live in the identity provider; the list shows token subjects.
- A user removed from a tenant can still hold a valid access token until it expires, but it grants nothing in that
  tenant. Re-joining needs a new invitation or assignment.
