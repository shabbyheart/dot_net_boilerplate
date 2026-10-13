# ADR-0007: Configuration and secrets

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate committed JWT keys, database credentials and passwords in `appsettings.*.json`,
re-added configuration sources after `CreateBuilder` (so files overrode user secrets), read raw
`IConfiguration` inside handlers, never validated settings at startup, and exited with code 0 when startup failed.

## Alternatives considered

- **Reading configuration:** raw `IConfiguration` lookups, `Get<T>()` at startup, or **the options pattern with validation**.
- **Secrets:** committed files, **user secrets in development and environment variables or a vault elsewhere**.
- **Secret detection:** code review only, or **an automated scanner in CI**.

## Decision

- Use the configuration sources set up by `WebApplication.CreateBuilder` without re-adding or reordering them.
- Settings are bound with the options pattern:
  `AddOptions<T>().BindConfiguration(section).ValidateDataAnnotations().ValidateOnStart()`.
  Code depends on `IOptions<T>` (or `IOptionsMonitor<T>` when reloads matter), never on `IConfiguration`
  outside the composition root. The first settings class arrives with the first real setting (M3, database).
- **No secrets in the repository.** `appsettings*.json` contain only non-secret defaults. Development secrets use
  `dotnet user-secrets`; deployed environments use environment variables or a secret store (decided in a later ADR, A14).
- CI runs **gitleaks** over the full git history on every push and pull request. Any finding fails the build.
  The official release binary is downloaded and verified against its published checksum; the GitHub Action
  wrapper is not used because it requires a licence key for organisation accounts.
- If the host fails to start, the process logs the failure and **exits with code 1**, so orchestrators and
  scripts see the failure.

## Consequences

- Misconfiguration fails at startup instead of on the first request that needs the setting.
- Contributors must set up user secrets locally once settings with secrets exist; the README will list them.
- A false positive from gitleaks needs a reviewed allowlist entry (`.gitleaks.toml`), never a disabled step.
