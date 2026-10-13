# ADR-0016: Authentication

- Status: Accepted (token validation). Who issues production tokens is decided before deployment.
- Date: 2026-10-13

## Context

The previous boilerplate issued its own long-lived JWTs (7-day access, 365-day refresh), did not validate
issuer or audience, threw exceptions from authentication events, and kept signing keys in committed settings.
Tenancy (ADR-0010) needs an authenticated user with a tenant claim, so authentication comes before the first
tenant-owned feature. The organisation has not yet chosen between an external identity provider and issuing tokens locally.

## Alternatives considered

1. **External OpenID Connect provider** (Keycloak, Entra ID, Auth0): the API only validates tokens.
2. **Local issuer** (ASP.NET Core Identity plus self-issued JWTs and refresh tokens).
3. **Validation now, issuer later:** the API validates standard JWT bearer tokens configured through
   framework options; either issuer can be plugged in by configuration.
4. **Custom JWT options and validation code** (the previous approach).

## Decision

Option 3.

- `AddAuthentication().AddJwtBearer()` with the framework's `JwtBearerOptions`, bound from
  `Authentication:Schemes:Bearer`. No custom options class.
  - External provider: set `Authority` (signing keys come from its metadata and rotate automatically) and `ValidAudiences`.
  - Signing keys: set `ValidIssuer`, `ValidAudiences` and `SigningKeys` (this is what `dotnet user-jwts` writes for development).
- Issuer, audience, lifetime and signature are always validated. `MapInboundClaims = false`, so claims keep their
  JWT names (`sub`, `tenant_id`). Clock skew is one minute.
- The process **does not start** without an audience and either an `Authority` or an issuer plus signing keys (ADR-0007).
- **Deny by default:** the fallback authorization policy requires an authenticated user with a `sub` claim.
  Only health checks and the Development-only API documentation allow anonymous access.
- `ICurrentUser` (Application) exposes the `sub` claim as the user id (a string, at most 200 characters, because
  external providers use different formats). `ITenantContext` keeps reading `tenant_id` (ADR-0010).
- `CreatedBy` and `UpdatedBy` on auditable entities record the user id; operations without a user store `null`.
  `AuditInterceptor` sets them together with the timestamps (it was called `AuditTimestampsInterceptor` in ADR-0011 and ADR-0012).
- A post-configuration step sets issuer, audience, lifetime and signing-key validation on explicitly. The framework
  already enables them by default (verified for configuration with only an `Authority`); the explicit setting is
  defence in depth against changed defaults or configuration.
- 401 and 403 responses are ProblemDetails (ADR-0006). Authentication events never throw.
- Development tokens: `dotnet user-jwts create --project src/Boilerplate.Api --claim tenant_id=<guid>`.
- Tests sign tokens with a random key generated per test run; no key is ever committed.

## Consequences

- Any standards-compliant issuer works without code changes.
- Until a production issuer is chosen, only development and test tokens exist.
- Permissions and roles are not part of this decision (ADR-0017).
