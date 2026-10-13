# ADR-0010: Tenancy model

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate was named "multitenancy shared schema" but had no tenant isolation: no `TenantId`
on business data, no query filters, tenant tables missing from the database model, and an optional tenant
header. Retrofitting tenancy later touches every entity, index, query and test, so the model must be
decided before the persistence foundation.

## Alternatives considered

- **T1, no tenancy:** simplest; multi-tenant applications must retrofit it.
- **T2, always multi-tenant, shared schema:** every application is multi-tenant; a single-tenant deployment
  runs with one tenant. One code path.
- **T3, tenancy as an opt-in capability per application:** two variants to maintain and test.
- Isolation styles considered: shared schema with a `TenantId` column, schema per tenant, database per tenant.

## Decision

**T2, shared schema.**

- `Tenant` is an entity in Domain. Tenant-owned entities implement `ITenantOwned` (`Guid TenantId`).
- `ITenantContext` (Application) provides the current tenant id, or none.
  - In HTTP requests it comes from the authenticated user's **`tenant_id` claim** (`ClaimsTenantContext` in Api).
    How the claim is issued is decided with the authentication strategy (ADR-0016).
  - Background work (from M9) sets the tenant explicitly from the job payload.
- `AppDbContext` applies, to every `ITenantOwned` entity automatically:
  - a **named global query filter** (`"Tenant"`): rows are visible only when `TenantId` equals the current tenant;
  - a required foreign key to `tenants` and an index on `tenant_id`.
- `TenantStampingInterceptor` on every save:
  - new tenant-owned rows get the current tenant id; a different explicit tenant id is rejected;
  - updates and deletes of another tenant's rows are rejected; changing `TenantId` is rejected.
- **Fail closed:** with no current tenant, tenant-owned queries return no rows and tenant-owned writes throw.
- Unique constraints on tenant-owned data are composite, starting with `TenantId`.
- `IgnoreQueryFilters()` is allowed only in an explicit, reviewed platform-administration path (none exists yet).

**Known gaps the filter does not cover, and how they are handled:**

| Path | Behaviour | Control |
|---|---|---|
| `ExecuteUpdate` / `ExecuteDelete` | Query filters apply; interceptors do not run | Allowed; covered by isolation tests |
| Raw SQL (`ExecuteSql*`, `FromSql` not composed) | Filters do not apply | Only in Infrastructure, with an explicit tenant parameter and a cross-tenant test |
| Caches | Not filtered | Cache keys include the tenant id |
| Background jobs | No HTTP user | Job payload carries the tenant; a job without one fails |

## Consequences

- Every tenant-owned table carries `tenant_id`, even in single-tenant deployments.
- Isolation is enforced centrally and tested; features cannot forget it for normal LINQ queries.
- Until authentication exists (M5) there is never a current tenant, so tenant-owned data is unreachable over HTTP.
