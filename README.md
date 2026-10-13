# Boilerplate

A reusable ASP.NET Core backend foundation: four projects (Domain, Application, Infrastructure, Api)
with vertical feature slices, enforced dependency rules, and tests from the first commit.

Status: **M3, persistence foundation.** The API has structured logging, a ProblemDetails error contract,
liveness and readiness probes, and (in Development) an OpenAPI document. It stores data in PostgreSQL through
EF Core, with tenant isolation built in. It ships as a non-root container image, and a compose stack runs it
locally with PostgreSQL. Business features arrive in later milestones.

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

# 4. Run the API (Development, http://localhost:5080)
dotnet run --project src/Boilerplate.Api
```

Add a migration after changing the model:

```bash
dotnet ef migrations add <Name> --project src/Boilerplate.Infrastructure --output-dir Persistence/Migrations
```

## Containers

```bash
# API image (non-root, chiseled runtime); needs a database connection string to start
docker build -f deploy/Dockerfile -t boilerplate-api .

# Local stack: PostgreSQL 18 + API (Development settings, Scalar at http://localhost:8080/scalar)
cp deploy/.env.example deploy/.env                # then set POSTGRES_PASSWORD
docker compose -f deploy/compose.yaml up -d --build
docker compose -f deploy/compose.yaml down        # add --volumes to delete the database
```

Both bind to `127.0.0.1` only. If port 5432 or 8080 is already used on your machine (for example by a local
PostgreSQL), set `POSTGRES_PORT` or `API_PORT` in `deploy/.env` and use that port in your connection string.
See [ADR-0018](docs/adr/0018-containers-and-ci.md).

## Endpoints

| Path | Environment | Purpose |
|---|---|---|
| `/health/live` | All | Liveness: the process can serve requests (no dependency checks) |
| `/health/ready` | All | Readiness: the database is reachable |
| `/openapi/v1.json` | Development | OpenAPI document |
| `/scalar` | Development | Interactive API reference |

Errors use RFC 9457 ProblemDetails with a `traceId` (see [ADR-0006](docs/adr/0006-error-handling-problem-details.md)).

## Configuration and secrets

Never commit secrets. `appsettings*.json` hold non-secret defaults only; use `dotnet user-secrets` in
development and environment variables in deployed environments ([ADR-0007](docs/adr/0007-configuration-and-secrets.md)).
CI scans the full git history with gitleaks.

| Setting | Required | Source |
|---|---|---|
| `Database:ConnectionString` (`Database__ConnectionString`) | Yes; startup fails without it | User secrets or environment variable |

## Repository layout

```
src/        Boilerplate.Domain, .Application, .Infrastructure, .Api
tests/      Boilerplate.UnitTests, .IntegrationTests, .ArchitectureTests
deploy/     Dockerfile, compose.yaml, .env.example
docs/       architecture.md, adr/ (architecture decision records)
```

- [Architecture and dependency rules](docs/architecture.md)
- [Architecture decision records](docs/adr/README.md)
- [Contributor and agent guidelines](AGENTS.md)

## Troubleshooting

**"An Application Control policy has blocked this file" (Windows).** Smart App Control or a Windows
Defender Application Control policy blocks unsigned, newly built DLLs from loading. The architecture tests
avoid loading the layer assemblies, but running the API and the integration tests needs them to load.
Options: run on a machine or VM where the policy is off, use WSL or a dev container, or ask your administrator
for a policy exception for your development folder.
