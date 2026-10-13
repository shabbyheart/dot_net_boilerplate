# Boilerplate

A reusable ASP.NET Core backend foundation: four projects (Domain, Application, Infrastructure, Api)
with vertical feature slices, enforced dependency rules, and tests from the first commit.

Status: **M7, role administration.** The API has structured logging, a ProblemDetails error contract, liveness
and readiness probes, and (in Development) an OpenAPI document. Every endpoint requires a valid JWT bearer token
unless it is explicitly anonymous, and endpoints can require permissions granted through tenant-scoped roles,
checked on every request so revocation is immediate. Roles are administered over HTTP with an escalation guard,
and a one-off `bootstrap` command creates a tenant and its first administrator. It stores data in PostgreSQL
through EF Core, with tenant isolation driven by the token's `tenant_id` claim. A small `projects` feature shows
how every feature is built ([how to add a feature](docs/how-to-add-a-feature.md)). It ships as a non-root container
image, and a compose stack runs it locally with PostgreSQL.

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

In other environments, configure the issuer through `Authentication:Schemes:Bearer`: `Authority` and
`ValidAudiences` for an OpenID Connect provider ([ADR-0016](docs/adr/0016-authentication.md)).

Add a migration after changing the model:

```bash
dotnet ef migrations add <Name> --project src/Boilerplate.Infrastructure --output-dir Persistence/Migrations
```

## Containers

```bash
# API image (non-root, chiseled runtime); needs database and authentication settings to start
docker build -f deploy/Dockerfile -t boilerplate-api .

# Local stack: PostgreSQL 18 + API (Development settings, Scalar at http://localhost:8080/scalar)
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
| `/health/live` | All | Anonymous | Liveness: the process can serve requests (no dependency checks) |
| `/health/ready` | All | Anonymous | Readiness: the database is reachable |
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
| `Authentication:Schemes:Bearer:ValidAudiences` | Yes; startup fails without it | Configuration or environment variable |
| `Authentication:Schemes:Bearer:Authority`, or `ValidIssuer` with `SigningKeys` | Yes; startup fails without one | Configuration; signing keys only in user secrets (development) |

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
