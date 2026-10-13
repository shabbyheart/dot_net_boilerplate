# Boilerplate

A reusable ASP.NET Core backend foundation: four projects (Domain, Application, Infrastructure, Api)
with vertical feature slices, enforced dependency rules, and tests from the first commit.

Status: **M0, repository and quality gates.** The API starts and does nothing yet. Features arrive in later milestones.

## Requirements

- .NET 10 SDK (pinned by `global.json`; any 10.0 feature band from 10.0.100 works)

## Commands

```bash
dotnet restore
dotnet format --verify-no-changes   # formatting and code style
dotnet build -c Release             # warnings are errors
dotnet test -c Release              # Microsoft.Testing.Platform runner
dotnet run --project src/Boilerplate.Api
```

## Repository layout

```
src/        Boilerplate.Domain, .Application, .Infrastructure, .Api
tests/      Boilerplate.UnitTests, Boilerplate.ArchitectureTests
docs/       architecture.md, adr/ (architecture decision records)
```

- [Architecture and dependency rules](docs/architecture.md)
- [Architecture decision records](docs/adr/README.md)
- [Contributor and agent guidelines](AGENTS.md)

## Troubleshooting

**"An Application Control policy has blocked this file" (Windows).** Smart App Control or a Windows
Defender Application Control policy blocks unsigned, newly built DLLs from loading. The architecture tests
avoid loading the layer assemblies, but running the API or future integration tests needs them to load.
Options: run on a machine or VM where the policy is off, use WSL or a dev container, or ask your administrator
for a policy exception for your development folder.
