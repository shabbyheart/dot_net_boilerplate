# ADR-0027: Platform administration and invitations

- Status: Accepted
- Date: 2026-10-14

## Context

Tenants can only be created by the `bootstrap` command (ADR-0022), and giving someone a role requires knowing their
token subject, which users rarely know and administrators cannot look up (there is no user table). An operator who
runs the service for many customers needs to create and rename tenants over HTTP, and tenant administrators need a
way to bring people in by e-mail address.

## Alternatives considered

- **Who administers tenants:** the command line only, a role inside a special "platform" tenant (mixes two scopes and
  turns tenant roles into a global power), or **a separate platform administrator grant**, independent of tenants.
- **Granting platform administration:** over HTTP by other platform administrators (one stolen token can create
  more), or **only by a command run as an operational step**, like `bootstrap`.
- **Platform administrators inside tenants:** implicit access to every tenant's data, or **none: they create tenants
  and invite tenant administrators**, who then manage the tenant.
- **Accepting an invitation:** the accepting token must carry a verified e-mail claim matching the invitation (strong,
  but many identity providers emit no verified e-mail claim, and local tokens carry none), or **any signed-in user who
  presents the single-use token from the e-mail** (the common pattern for team invitations).
- **Accept page setting:** required at startup, or **optional, with invitation creation refused (409) until it is set**.

## Decision

**Platform administrators**

- `platform_administrators` (not tenant-owned) lists the token subjects who are platform administrators. The grant is
  checked by `[RequirePlatformAdministrator]` with one indexed query per request and no cache, so revoking it is
  effective on the next request (as ADR-0017). Without the grant: 403; without a token: 401.
- Granted and revoked only by `platform-admin grant|revoke --user <subject>`, run like `bootstrap`. No endpoint can
  create a platform administrator.
- The grant is unrelated to tenant roles: it gives no permission inside any tenant, and a tenant administrator is not
  a platform administrator. A platform administrator's token need not name a tenant.

**Tenant administration** (`/api/v1/platform/tenants`, platform administrators only)

- List (by name, paged), get, create, rename. Names stay unique (ADR-0022).
- Creating a tenant creates its administrator role and an invitation to that role for the given e-mail address,
  in one transaction. `POST {id}/administrator-invitations` sends another (for example after the first expired).
- These handlers act on the target tenant through `TenantOverride` (ADR-0022), which HTTP requests may now set too.
- Deleting tenants is not offered: it would cascade through every feature's data and needs its own decision.

**Invitations** (`/api/v1/invitations`, tenant-owned)

- Creating, listing and revoking need `roles.manage` / `roles.read`. Inviting to a role passes the same escalation
  guard as assigning it (ADR-0022): the caller must hold all its permissions, and only administrators can invite to
  the administrator role. No new permission is introduced.
- An invitation stores the lower-cased address, the role, a SHA-256 hash of a 256-bit random token, and its expiry
  (`Invitations:Lifetime`, 7 days by default). The token exists only in the e-mail, which goes through the outbox
  (ADR-0019) and links to `Invitations:AcceptUrl` with a `token` parameter.
- A new invitation of the same address to the same role revokes earlier pending ones.
- `POST /api/v1/invitations/accept` (any signed-in user, any or no tenant in the token): the caller receives the role
  in the invitation's tenant and the invitation records who accepted it. Unknown, used, revoked and expired tokens get
  the same 400. A row version stops concurrent double acceptance (409). The lookup by token hash is a reviewed bypass
  of the tenant filter (ADR-0010). After accepting, the caller signs in for that tenant (local accounts: `tenantId`).
- **Whoever presents the link accepts it.** The address is where the link is sent, not a check on who accepts. The
  e-mail says so, invitations can be revoked until accepted, and `acceptedBy` shows who accepted.
- Deleting a role deletes its invitations. Expired invitations are removed by housekeeping (ADR-0026) after the
  token retention.
- Without `Invitations:AcceptUrl` the API starts, and creating invitations or tenants answers 409. A value that is set
  must be an absolute http(s) URL, checked at startup.

## Consequences

- An operator can onboard customers over HTTP without knowing anyone's token subject, and tenants can onboard their
  own people by e-mail address, with the same escalation rules as role assignment.
- The platform administrator grant is a powerful, tenant-independent privilege. It cannot be granted over HTTP, and
  every grant is visible in one small table.
- A forwarded or intercepted invitation e-mail grants the role to whoever uses it first. Deployments that need a
  stronger binding need verified e-mail claims from their identity provider and a superseding decision.
- Invitations are checked against the inviter's permissions when issued, not again when accepted.
- External identity providers decide which `tenant_id` a token carries; accepting an invitation creates the role
  assignment, but making the provider issue tokens for that tenant is the provider's configuration.
