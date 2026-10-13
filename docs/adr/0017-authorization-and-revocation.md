# ADR-0017: Authorization and revocation

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate had a good idea, permission-based policies, implemented with three defects:
permission names were validated case-insensitively but checked case-sensitively; soft-deleted role permissions
stayed effective, so revoking a permission did not revoke it; and deactivated users kept access. Authentication
(ADR-0016) identifies the caller; this decision covers what the caller may do, within their tenant (ADR-0010).

## Alternatives considered

- **Model:** role checks only (coarse); permissions embedded in the token (stale until it expires, large tokens);
  **permissions granted through tenant-scoped roles, stored in the database**.
- **Endpoint declaration:** one named policy per permission; a custom `IAuthorizationPolicyProvider`;
  **an attribute implementing `IAuthorizationRequirementData`** (.NET 8+), which supplies the requirement directly.
- **Revocation (A15):** local cache with a time-to-live (bounded delay); local cache with a version stamp;
  distributed cache; **no cache: one indexed query per check**.
- **Abstraction:** an `IPermissionSource` interface (for a future identity-provider claims source), or **a concrete
  service until a second source exists**.

## Decision

- `Role` (tenant-owned aggregate) holds a set of permission names; `RoleAssignment` (tenant-owned) links a user id
  (the token's `sub`) to a role. Both are subject to the tenant query filter, so roles never apply across tenants.
- Permission names have one canonical form, `area.action` in lowercase (`[a-z0-9-]+(\.[a-z0-9-]+)+`, at most
  100 characters), enforced by `Role`. Checks compare ordinally. Different spellings cannot exist.
- Removing a permission or an assignment deletes the row. There is no soft delete for authorization data.
- `PermissionChecker` (Application) answers "does the current user hold this permission in the current tenant"
  with one indexed query. **No cache: a revocation is effective on the next request on every instance.**
  Caching may be added later only with measurements and a superseding ADR that states the revocation guarantee.
- Endpoints declare `[RequirePermission("area.action")]`. The attribute implements `IAuthorizationRequirementData`;
  `PermissionAuthorizationHandler` evaluates it. The default policy (used with any `[Authorize]`-style attribute)
  is the same as the fallback policy: an authenticated user with a `sub` claim.
- Results: anonymous → 401; authenticated without the permission, or without a tenant → 403. Both are ProblemDetails.
- Permission constants are added by the feature that introduces them (role administration in M7, the reference
  feature in M4), not ahead of time.
- No `IPermissionSource` interface yet: there is one source. If permissions must also come from identity-provider
  claims, introduce the interface then, with both implementations.

## Consequences

- Revocation is immediate and needs no cache invalidation design.
- Every protected request runs one small query (indexed by tenant, user and role). Measure before optimising.
- Deactivating a user means removing their role assignments (role administration, M7); the identity provider
  remains responsible for disabling sign-in.
- Escalation guards (granting only permissions you hold) belong to role administration (M7).
