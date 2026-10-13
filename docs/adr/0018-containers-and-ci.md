# ADR-0018: Containers and CI

- Status: Accepted (deployment and CD remain open until the deployment target is chosen)
- Date: 2026-10-13

## Context

The previous boilerplate's Dockerfile ran as root, copied an incomplete set of project files into its restore
layer, baked `appsettings` files with secrets into the image, and had no automated check that the image worked.
There was no CI at all.

## Alternatives considered

- **Runtime image:** full `aspnet` (Debian/Ubuntu with shell), Alpine, or **Ubuntu chiseled** (no shell,
  no package manager, non-root by default).
- **Chiseled variant:** plain chiseled (no ICU or time zone data; requires invariant globalization) or
  **chiseled-extra** (includes ICU and tzdata).
- **Image tags:** floating major (`10.0`), exact patch, or digest pinning.
- **Container health:** Docker `HEALTHCHECK` (needs a tool inside the image) or **orchestrator probes** on `/health/*`.
- **Vulnerable package gate:** a separate `dotnet list package --vulnerable` step, or **NuGet audit during every restore**.

## Decision

- `deploy/Dockerfile`, multi-stage:
  - build stage `mcr.microsoft.com/dotnet/sdk:10.0-noble`; the restore layer copies `global.json`,
    `Directory.Build.props`, `Directory.Packages.props` and every source `.csproj` the API depends on, so it is
    cached until dependencies change;
  - runtime stage `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra`, running as the image's
    non-root `app` user (`USER $APP_UID`), listening on port 8080;
  - configuration comes only from environment variables; `.dockerignore` keeps tests, docs, git data,
    local settings and `deploy/.env` out of the build context.
- Images use floating `10.0` tags so rebuilds pick up security patches. Digest pinning with automated
  update PRs can be added later.
- No `HEALTHCHECK` instruction: chiseled images have no shell or curl. Orchestrators probe `/health/live`
  (and `/health/ready` from M3).
- `deploy/compose.yaml` runs the local stack: PostgreSQL (ADR-0009) and the API. Ports bind to `127.0.0.1` only.
  The database password comes from `deploy/.env`, which is git-ignored; `deploy/.env.example` documents it.
- **NuGet audit** is enabled explicitly (`NuGetAudit`, `NuGetAuditMode=all`) and, with warnings as errors,
  every restore fails on a known vulnerability in a direct or transitive package. A vulnerability that cannot
  be fixed yet needs a `NuGetAuditSuppress` entry with a reason and an issue reference.
- CI (GitHub Actions) jobs:
  - `build-and-test`: restore (includes the audit), format check, build, tests;
  - `secret-scan`: gitleaks over the full history (ADR-0007);
  - `container`: builds the image, fails if it runs as root, starts it and probes `/health/live`, checks that
    OpenAPI is not exposed in Production, then starts the compose stack and probes it.
- CI also runs weekly on a schedule, so newly published advisories are caught without a code change.

## Consequences

- No shell in the runtime image: debugging uses `docker logs`, or a debug image or sidecar.
- chiseled-extra is larger than plain chiseled, in exchange for working cultures and time zones.
- Floating tags mean two builds of the same commit can differ by a patch release.
- Continuous deployment is out of scope until the deployment target, secret store and TLS termination are decided (A14).
