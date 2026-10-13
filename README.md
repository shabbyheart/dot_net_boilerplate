# Boilerplate

A reusable ASP.NET Core backend foundation: four projects (Domain, Application, Infrastructure, Api)
with vertical feature slices, enforced dependency rules, and tests from the first commit.

Status: **M1, host skeleton.** The API starts with structured logging, a ProblemDetails error contract, a liveness
probe and (in Development) an OpenAPI document. Business features arrive in later milestones.

## Requirements

- .NET 10 SDK (pinned by `global.json`; any 10.0 feature band from 10.0.100 works)
- Docker with Compose v2, for the container image and the local stack (and for integration tests from M3)

## Commands

```bash
dotnet restore
dotnet format --verify-no-changes   # formatting and code style
dotnet build -c Release             # warnings are errors
dotnet test -c Release              # Microsoft.Testing.Platform runner
dotnet run --project src/Boilerplate.Api  # Development, http://localhost:5080
```

## Endpoints

| Path | Environment | Purpose |
|---|---|---|
| `/health/live` | All | Liveness: the process can serve requests (no dependency checks) |
| `/openapi/v1.json` | Development | OpenAPI document |
| `/scalar` | Development | Interactive API reference |

Errors use RFC 9457 ProblemDetails with a `traceId` (see [ADR-0006](docs/adr/0006-error-handling-problem-details.md)).

## Configuration and secrets

Never commit secrets. `appsettings*.json` hold non-secret defaults only; use `dotnet user-secrets` in
development and environment variables in deployed environments ([ADR-0007](docs/adr/0007-configuration-and-secrets.md)).
CI scans the full git history with gitleaks.

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
