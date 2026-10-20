# Boilerplate

A reusable ASP.NET Core backend foundation: four projects (Domain, Application, Infrastructure, Api)
with vertical feature slices, enforced dependency rules, and tests from the first commit.

Status: **all planned milestones done (M0 to M10), plus M11 housekeeping, M12 platform administration, M13 audit log and M14 tenant members.** The API accepts tokens from an external OpenID Connect provider
and can optionally issue its own through local accounts (registration, e-mail confirmation, sign-in with lockout,
rotating refresh tokens, password reset). It has OpenTelemetry tracing and metrics, rate limiting, security headers,
CORS and proxy support. It also has
structured logging, a ProblemDetails error contract, liveness
and readiness probes, and (in Development) an OpenAPI document. Every endpoint requires a valid JWT bearer token
unless it is explicitly anonymous, and endpoints can require permissions granted through tenant-scoped roles,
checked on every request so revocation is immediate. Roles are administered over HTTP with an escalation guard,
and a one-off `bootstrap` command creates a tenant and its first administrator. Platform administrators (granted by
the `platform-admin` command) create and rename tenants over HTTP, and people join tenants through e-mailed
invitations; administrators list and remove members, and members can leave a tenant. Changes to roles, assignments, invitations and tenants are recorded in an append-only, tenant-scoped
audit log that administrators can read over HTTP. It stores data in PostgreSQL
through EF Core, with tenant isolation driven by the token's `tenant_id` claim. Side effects such as e-mail go
through a transactional outbox and are delivered by a background dispatcher over SMTP; a housekeeping job deletes
expired tokens and delivered messages after a retention period. A small `projects` feature
shows how every feature is built ([how to add a feature](docs/how-to-add-a-feature.md)). It ships as a non-root
container image, and a compose stack runs it locally with PostgreSQL and Mailpit.

## Requirements

- .NET 10 SDK (pinned by `global.json`; any 10.0 feature band from 10.0.100 works)
- Docker with Compose v2: for the container image, the local stack and the integration tests (Testcontainers)

## Commands

```bash
dotnet restore
dotnet tool restore                 # dotnet-ef, for migrations
dotnet format --verify-no-changes   # formatting and code style
dotnet build -c Release             # warnings are errors
dotnet test -c Release              # Microsoft.Testing.Platform runner; integration tests need Docker
```

## Running locally

The API needs a PostgreSQL connection string (`Database:ConnectionString`). It is a secret, so it never goes
into `appsettings` files.

```bash
# 1. Start PostgreSQL (and the API container) with compose
cp deploy/.env.example deploy/.env                # then set POSTGRES_PASSWORD (deploy/.env is git-ignored)
docker compose -f deploy/compose.yaml up -d db

# 2. Store the connection string as a user secret for the API project
dotnet user-secrets init --project src/Boilerplate.Api
dotnet user-secrets set "Database:ConnectionString" "Host=localhost;Database=boilerplate;Username=boilerplate;Password=<your password>" --project src/Boilerplate.Api

# 3. Create or update the schema (the API never migrates on startup, ADR-0011)
dotnet tool restore
Database__ConnectionString="<same connection string>" dotnet ef database update --project src/Boilerplate.Infrastructure

# 4. Create a tenant and make yourself its administrator (idempotent; prints the tenant id)
dotnet run --project src/Boilerplate.Api -- bootstrap --tenant-name "Acme" --admin-user <your token subject>

# 5. Create a development token for that subject and tenant. This also writes the issuer and audience the API
#    accepts (signing key in user secrets, issuer and audience in appsettings.Development.json).
dotnet user-jwts create --project src/Boilerplate.Api --name <your token subject> --claim tenant_id=<tenant id>

# 6. Run the API (Development, http://localhost:5080) and call it with the token
dotnet run --project src/Boilerplate.Api
curl -H "Authorization: Bearer <token>" http://localhost:5080/api/v1/roles
```

The bootstrap command runs after migrations as a deployment step, never on startup ([ADR-0022](docs/adr/0022-role-administration-and-bootstrap.md)).
In a container: `docker compose -f deploy/compose.yaml run --rm api bootstrap --tenant-name "Acme" --admin-user <subject>`.

To administer tenants over HTTP, make a user a platform administrator with the `platform-admin` command (also a
deployment step; there is no endpoint for it, [ADR-0027](docs/adr/0027-platform-administration-and-invitations.md)):

```bash
dotnet run --project src/Boilerplate.Api -- platform-admin grant --user <token subject>
dotnet run --project src/Boilerplate.Api -- platform-admin revoke --user <token subject>
```

A platform administrator creates a tenant with `POST /api/v1/platform/tenants` and an e-mail address; that address
receives an invitation to the tenant's administrator role. Set `Invitations:AcceptUrl` to the client page that
reads the `token` from the link and posts it to `/api/v1/invitations/accept` for the signed-in user. Anyone who has
the link can accept it, so it is single use, expires (7 days) and can be revoked.

In other environments, configure the issuer through `Authentication:Schemes:Bearer`: `Authority` and
`ValidAudiences` for an OpenID Connect provider ([ADR-0016](docs/adr/0016-authentication.md)).

### Local accounts (optional)

Without an external provider, the API can manage users itself ([ADR-0025](docs/adr/0025-local-accounts.md)):

```bash
# Enable accounts; the signing key is a secret (32+ random bytes, base64)
dotnet user-secrets set "Accounts:Tokens:SigningKey" "$(openssl rand -base64 32)" --project src/Boilerplate.Api
# Non-secret settings (or put them in appsettings): issuer and audience of the tokens, and the client pages
# that confirmation and reset e-mails link to
Accounts__Enabled=true \
Accounts__Tokens__Issuer=http://localhost:5080 Accounts__Tokens__Audience=boilerplate-api \
Accounts__EmailConfirmationUrl=http://localhost:3000/confirm-email \
Accounts__PasswordResetUrl=http://localhost:3000/reset-password \
dotnet run --project src/Boilerplate.Api
```

Register (`POST /api/v1/account/register`), confirm with the token from the e-mail in Mailpit, sign in without a
tenant and read your user id from `GET /api/v1/me`. Use that id with `bootstrap --admin-user`, then sign in again with
`tenantId` to act for the tenant.

Add a migration after changing the model:

```bash
dotnet ef migrations add <Name> --project src/Boilerplate.Infrastructure --output-dir Persistence/Migrations
```

## Containers

```bash
# API image (non-root, chiseled runtime); needs database and authentication settings to start
docker build -f deploy/Dockerfile -t boilerplate-api .

# Local stack: PostgreSQL 18 + API (Development settings, Scalar at http://localhost:8080/scalar)
# + Mailpit, which captures all e-mail (UI at http://localhost:8025)
cp deploy/.env.example deploy/.env                # then set POSTGRES_PASSWORD
docker compose -f deploy/compose.yaml up -d --build
docker compose -f deploy/compose.yaml down        # add --volumes to delete the database
```

Both bind to `127.0.0.1` only. If port 5432 or 8080 is already used on your machine (for example by a local
PostgreSQL), set `POSTGRES_PORT` or `API_PORT` in `deploy/.env` and use that port in your connection string.
The API container accepts tokens only after you set `AUTH_AUTHORITY` (and optionally `AUTH_AUDIENCE`) in
`deploy/.env`; `dotnet user-jwts` tokens work only for the API run with `dotnet run`.
See [ADR-0018](docs/adr/0018-containers-and-ci.md).

## Endpoints

| Path | Environment | Access | Purpose |
|---|---|---|---|
| `/api/v1/me` | All | Token | The caller's user id (`sub`) and tenant id (`tenant_id`) |
| `GET /api/v1/projects?page=&pageSize=` | All | `projects.read` | The tenant's projects, ordered by name, paged (ADR-0021) |
| `GET /api/v1/projects/{id}` | All | `projects.read` | One project |
| `POST /api/v1/projects` | All | `projects.manage` | Create (201 with `Location`; 409 on a duplicate name) |
| `PUT /api/v1/projects/{id}` | All | `projects.manage` | Replace name and description |
| `DELETE /api/v1/projects/{id}` | All | `projects.manage` | Delete (204) |
| `GET /api/v1/permissions` | All | `roles.read` | Every permission a role can grant |
| `GET /api/v1/roles`, `GET /api/v1/roles/{id}` | All | `roles.read` | Roles with their permissions (`isAdministrator` = all permissions) |
| `POST /api/v1/roles`, `PUT/DELETE /api/v1/roles/{id}` | All | `roles.manage` | Create, replace, delete; only permissions you hold (403 otherwise) |
| `GET /api/v1/roles/{id}/assignments` | All | `roles.read` | Users (token subjects) who hold the role |
| `PUT/DELETE /api/v1/roles/{id}/assignments/{userId}` | All | `roles.manage` | Assign (idempotent) or unassign; the last administrator stays (409) |
| `GET/POST /api/v1/invitations`, `GET/DELETE /api/v1/invitations/{id}` | All | `roles.read` / `roles.manage` | List, invite an address to a role (only roles you could assign), view, revoke ([ADR-0027](docs/adr/0027-platform-administration-and-invitations.md)) |
| `POST /api/v1/invitations/accept` | All | Token | Accept with the e-mailed token: the caller receives the role in the invitation's tenant |
| `GET/POST /api/v1/platform/tenants`, `GET/PUT /api/v1/platform/tenants/{id}` | All | Platform administrator | List, create (with an invitation to its first administrator), view, rename tenants |
| `POST /api/v1/platform/tenants/{id}/administrator-invitations` | All | Platform administrator | Invite another administrator to a tenant |
| `POST /api/v1/account/register`, `confirm-email` | Accounts enabled | Anonymous | Create an account (always 202); confirm the address with the e-mailed token |
| `POST /api/v1/account/login` | Accounts enabled | Anonymous | Access and refresh tokens; optional `tenantId` (the user must hold a role there) |
| `POST /api/v1/account/refresh`, `logout` | Accounts enabled | Anonymous | Rotate the refresh token (single use); end the session |
| `POST /api/v1/account/forgot-password`, `reset-password` | Accounts enabled | Anonymous | E-mail a reset link (always 202); set a new password and sign out everywhere |
| `/health/live` | All | Anonymous | Liveness: the process can serve requests (no dependency checks) |
| `/health/ready` | All | Anonymous | Readiness: the database is reachable and every migration is applied |
| `/openapi/v1.json` | Development | Anonymous | OpenAPI document |
| `/scalar` | Development | Anonymous | Interactive API reference |

Errors use RFC 9457 ProblemDetails with a `traceId` (see [ADR-0006](docs/adr/0006-error-handling-problem-details.md)).
Requests without a valid token get 401, including requests to paths that do not exist.

## Configuration and secrets

Never commit secrets. `appsettings*.json` hold non-secret defaults only; use `dotnet user-secrets` in
development and environment variables in deployed environments ([ADR-0007](docs/adr/0007-configuration-and-secrets.md)).
CI scans the full git history with gitleaks.

| Setting | Required | Source |
|---|---|---|
| `Database:ConnectionString` (`Database__ConnectionString`) | Yes; startup fails without it | User secrets or environment variable. In the container add `GSS Encryption Mode=Disable` unless you use Kerberos: the chiseled image has no Kerberos library |
| `Email:Smtp:Host`, `Email:Smtp:FromAddress` | Yes; startup fails without them | Configuration. Also `Port` (587), `Security` (`StartTls`; `None` only for Mailpit), `FromName` |
| `Email:Smtp:Username`, `Email:Smtp:Password` | If the server requires them | User secrets or environment variables |
| `Outbox:Enabled`, `PollingInterval`, `BatchSize`, `Lease`, `MaxAttempts` | No (defaults: on, 5 s, 20, 5 min, 8) | Configuration ([ADR-0019](docs/adr/0019-outbox-and-email.md)) |
| `Housekeeping:Enabled`, `Interval`, `BatchSize`, `ExpiredTokenRetention`, `ProcessedOutboxRetention`, `AuditLogRetention` | No (defaults: on, 1 h, 1000, 7 days, 7 days, 365 days) | Cleanup of expired tokens, delivered outbox messages and old audit log entries ([ADR-0026](docs/adr/0026-data-retention-housekeeping.md), [ADR-0028](docs/adr/0028-audit-log.md)) |
| `Invitations:AcceptUrl`, `Invitations:Lifetime` | No (default: none, 7 days); without the URL, creating invitations and tenants answers 409 | Client page that invitation e-mails link to (ADR-0027) |
| `Cors:AllowedOrigins` | No (default: none) | Browser origins allowed to call the API ([ADR-0024](docs/adr/0024-http-hardening.md)) |
| `RateLimiting:PermitLimit`, `RateLimiting:Window` | No (default: 100 per 10 s per caller) | Configuration (ADR-0024) |
| `ForwardedHeaders:KnownProxies`, `ForwardedHeaders:KnownNetworks` | Behind a proxy, yes | IPs / CIDR ranges of the TLS-terminating proxy. Without them the API sees the proxy's IP and `http` (ADR-0024) |
| `Kestrel:Limits:MaxRequestBodySize` | No (default: 1 MB) | Configuration (ADR-0024) |
| `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME`, ... | No (export off by default) | Standard OpenTelemetry variables ([ADR-0023](docs/adr/0023-tracing-and-metrics.md)) |
| `Authentication:Schemes:Bearer:ValidAudiences` | Yes; startup fails without it | Configuration or environment variable |
| `Authentication:Schemes:Bearer:Authority`, or `ValidIssuer` with `SigningKeys` | Yes, unless local accounts are enabled | Configuration; signing keys only in user secrets (development) |
| `Accounts:Enabled`, `EmailConfirmationUrl`, `PasswordResetUrl` | No (default: off); both URLs when on | Configuration: client pages the e-mailed links point to ([ADR-0025](docs/adr/0025-local-accounts.md)) |
| `Accounts:Tokens:Issuer`, `Audience`, `SigningKey` | When accounts are enabled | Configuration; `SigningKey` (base64, at least 32 random bytes) is a secret: user secrets or environment variable |
| `Accounts:Tokens:PreviousSigningKeys`, `Lifetime`; `Accounts:RefreshTokenLifetime`, ... | No (15 min access, 14 days refresh) | Configuration (ADR-0025) |
| `RateLimiting:AccountsPermitLimit`, `RateLimiting:AccountsWindow` | No (default: 10 per minute per IP) | Configuration (ADR-0025) |

For `dotnet run`, point `Email:Smtp` at Mailpit (`docker compose -f deploy/compose.yaml up -d mailpit`): host
`localhost`, port `1025`, security `None`.

## Repository layout

```
src/        Boilerplate.Domain, .Application, .Infrastructure, .Api
tests/      Boilerplate.UnitTests, .IntegrationTests, .ArchitectureTests
deploy/     Dockerfile, compose.yaml, .env.example
docs/       architecture.md, adr/ (architecture decision records)
```

- [Architecture and dependency rules](docs/architecture.md)
- [How to add a feature](docs/how-to-add-a-feature.md)
- [Architecture decision records](docs/adr/README.md)
- [Contributor and agent guidelines](AGENTS.md)

## Troubleshooting

**"An Application Control policy has blocked this file" (Windows).** Smart App Control or a Windows
Defender Application Control policy blocks unsigned, newly built DLLs from loading. The architecture tests
avoid loading the layer assemblies, but running the API and the integration tests needs them to load.
Options: run on a machine or VM where the policy is off, use WSL or a dev container, or ask your administrator
for a policy exception for your development folder.
