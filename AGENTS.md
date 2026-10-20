# AGENTS.md

Instructions for anyone changing this repository, human or AI agent. Read this file, then
[docs/architecture.md](docs/architecture.md) and the accepted ADRs in [docs/adr](docs/adr/README.md).
To add a feature, follow [docs/how-to-add-a-feature.md](docs/how-to-add-a-feature.md) and copy the projects feature.

## Commands

Run from the repository root. Work is not done until all of these pass.

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build -c Release
dotnet test -c Release
```

- Warnings are errors, locally and in CI.
- `dotnet test` uses Microsoft.Testing.Platform (opted in via `global.json`). Run one test project with
  `dotnet test --project tests/Boilerplate.ArchitectureTests`.
- Report what you ran and the observed result. Never claim a command passed without running it.

## Project map

| Path | Purpose |
|---|---|
| `src/Boilerplate.Domain` | Entities, domain rules, domain enums. No project or package references. |
| `src/Boilerplate.Application` | Use cases (`Features/<Area>/<UseCase>/`), ports, application exceptions, settings classes. |
| `src/Boilerplate.Infrastructure` | EF Core, migrations, implementations of Application ports. |
| `src/Boilerplate.Api` | Composition root, controllers (`Controllers/`), authentication, HTTP middleware, OpenAPI. |
| `tests/Boilerplate.UnitTests` | Domain and Application logic. |
| `tests/Boilerplate.IntegrationTests` | The real API in memory (`WebApplicationFactory`); real database from M3. |
| `tests/Boilerplate.ArchitectureTests` | Dependency and package rules. |
| `deploy/` | Dockerfile and local compose stack (PostgreSQL + API). |
| `docs/` | Architecture overview and ADRs. |

## Dependency rules

The rules in [docs/architecture.md](docs/architecture.md) are enforced by `Boilerplate.ArchitectureTests`.

- If an architecture test fails, fix the code, never the test. A rule changes only through a superseding ADR,
  and then the test changes in the same pull request.
- To decide where code belongs: if it decides **what** happens, it belongs in Application or Domain. If it
  decides **how**, with a specific technology, it belongs in Infrastructure. If it is still unclear, stop and ask.

## Packages

- Versions live only in `Directory.Packages.props`.
- A new package needs a reason recorded in an ADR: purpose, licence, alternatives (including "no package").
- Banned: `MediatR` (ADR-0004) and `Ardalis.*` (ADR-0002). Prefer the .NET base library and ASP.NET Core built-ins.
- Every restore runs NuGet audit; a known vulnerability fails the build. Fix it by upgrading. If that is impossible,
  add `NuGetAuditSuppress` with a reason and an issue reference (ADR-0018). Never disable the audit.

## Design rules

- SOLID is about clear responsibilities and localised change, not about the number of interfaces or rules.
- Add an interface only for an external boundary (I/O, vendor, clock), a test seam the real thing cannot
  provide, or a second implementation that exists today. Never for theoretical flexibility.
- Register services explicitly (ADR-0004). No assembly scanning, no marker interfaces.
- Use cases are plain handler classes with `HandleAsync(request, cancellationToken)`.
- Write queries in the handler. Extract an `IQueryable<T>` extension only when a second handler needs the same logic.
- Write mapping by hand (`Select` projections or small mapping methods). No mapping libraries.
- Write use cases have one clear commit point; read-only handlers never save.
- Typed exceptions are the default for failures that end a request (not found, conflict, forbidden, validation).
  Do not use exceptions for ordinary control flow or expected outcomes; return an explicit value instead.
- Aim for a dependency-free Domain. Adding a package to Domain is an architectural decision that needs an ADR.

## Coding conventions

- File-scoped namespaces; one public type per file, named after the type.
- Seal classes by default when nothing should inherit; allow inheritance when it has a clear purpose.
- `record` for requests, responses and other immutable data.
- Primary constructors for dependency injection.
- Async all the way: take a `CancellationToken` as the last parameter and pass it down. No `.Result`,
  `.Wait()` or `Task.Run` in request code.
- Use `TimeProvider`, never `DateTime.UtcNow` or `DateTimeOffset.UtcNow` directly.
- No static mutable state; no static logging or error helpers. Use `ILogger<T>`.
- Logging: message templates (`"Created role {RoleId}"`), never interpolated strings. Never log passwords,
  tokens, one-time codes, request bodies or authorization headers.
- Errors: throw the typed exceptions from `Boilerplate.Application.Common.Exceptions` (`NotFoundException` 404,
  `ConflictException` 409) or `DomainException` (400); `ApiExceptionHandler` maps them to ProblemDetails (ADR-0006).
  A new exception type needs a mapping and a test in the same change.
- Validation: one FluentValidation validator per request, registered explicitly; the MVC `ValidationFilter` runs it
  (ADR-0014). Do not add DataAnnotations or validate in controllers.
- Mapping: hand-written response records with a projection expression (ADR-0015). Lists are paged (ADR-0021).
- Configuration: bind settings with the options pattern and `ValidateOnStart()`; never read `IConfiguration`
  outside the composition root. Never commit secrets (ADR-0007).
- Comments explain why, not what.

## Persistence

- Handlers use `IAppDbContext` (ADR-0005). Add a `DbSet<T>` there only for aggregate roots.
- Entities derive from `Entity` (GUID v7 id). Auditable entities implement `IAuditable`; never set its timestamps yourself.
- Configuration goes in `Infrastructure/Persistence/Configurations/<Entity>Configuration.cs` (`IEntityTypeConfiguration<T>`).
- Every string property needs an explicit decision: `HasMaxLength(n)` for bounded values, or
  `HasColumnType("text")` for free-form text. `ModelConventionTests` fails on undecided strings.
- Add indexes for real query patterns, foreign keys and uniqueness constraints, with a short reason in the
  configuration. No blanket indexing.
- One commit point per write use case; read-only handlers never save; never hold a transaction across a remote call (ADR-0011).
- Migrations: `dotnet ef migrations add <Name> --project src/Boilerplate.Infrastructure --output-dir Persistence/Migrations`.
  Review the generated SQL shape. Never edit a migration that has been applied anywhere; add a new one.
  `MigrationTests` fails if the model changed without a migration.

## API and authentication (ADR-0016, ADR-0020)

- HTTP endpoints are MVC controllers in `src/Boilerplate.Api/Controllers`, routes under `/api/v1`, `[ApiController]`.
  Controllers stay thin: bind, call one Application handler, return the result.
- Every endpoint requires an authenticated user with a `sub` claim (fallback policy). Do not add `[AllowAnonymous]`
  or `.AllowAnonymous()` without a reason recorded in an ADR; today only health checks, Development documentation and
  the local account endpoints (ADR-0025) allow it.
- Use `ICurrentUser` for the caller and `ITenantContext` for the tenant. Never read claims or `HttpContext` in Application code.
- Never weaken token validation (issuer, audience, lifetime, signature) or add custom JWT parsing.

## Authorization (ADR-0017)

- Protect an endpoint that needs more than "any signed-in user" with `[RequirePermission("area.action")]`.
- Permission names are lowercase `area.action` (`PermissionName`). Add the constants with the feature that needs
  them, in a `<Area>Permissions` static class in Application, never ahead of time.
- Permissions are checked against the database on every request (no cache). Do not add caching without a
  superseding ADR that states the revocation guarantee.
- Authorization data is tenant-owned: roles and assignments never apply across tenants.
- Every permission constant is listed in `PermissionCatalog.All` (ADR-0022); `PermissionCatalogTests` enforces it.
- Role administration must go through `RoleEscalationGuard`; never let a caller grant, remove or hand out a
  permission they do not hold. Use `ForbiddenException` (403) for such refusals.
- Work without an HTTP user (commands, background jobs), platform administration and invitation acceptance set the
  tenant with `TenantOverride`, never by faking claims. Set it before the first write for that tenant.
- Platform endpoints use `[RequirePlatformAdministrator]`. Never add an endpoint that grants platform administration,
  and never let the platform grant open data inside tenants (ADR-0027).
- Inviting someone to a role is handing it out: go through `RoleEscalationGuard` like role assignment.
- Membership is derived from role assignments (ADR-0029); do not add a separate membership table. Removing a member
  passes the escalation guard for every role they hold, and nothing may remove the tenant's last administrator.

## Local accounts (ADR-0025)

- Account code goes through `IUserAccounts`; never use `UserManager` or Identity types outside Infrastructure.
- Identity must not save on its own: keep `UnitOfWorkUserStore`, and let the use case call `SaveChangesAsync` once.
- Store only hashes of tokens handed to clients (`SecureToken.Hash`). Never log tokens, passwords or account e-mails.
- Responses must not reveal whether an address is registered: same status and message for unknown, wrong and locked.
- Account endpoints keep `[RequireAccountsEnabled]` and the `accounts` rate-limit policy. Do not lengthen token
  lifetimes or weaken lockout and password rules without a superseding ADR.
- Two-factor sign-in (ADR-0031): reset the failed-attempt count only after every factor succeeded; wrong codes count
  towards lockout. Store recovery codes only as `RecoveryCodes.Hash`. Changes to two-factor settings need the current
  password, and switching off or renewing codes also needs a current code. Never let e-mail alone remove the second factor.

## Operations (ADR-0023, ADR-0024)

- Read settings through options or lazily when options are built, never eagerly while registering services:
  hosts and tests add configuration after registration.
- Composition code that uses Infrastructure types lives in `Program.cs` or `Boilerplate.Api.Registration`
  (enforced by `LayerDependencyTests`).
- Do not exempt endpoints from rate limiting, weaken security headers or trust more proxies without an ADR.
- New dependencies with OpenTelemetry instrumentation (HTTP clients, databases) are traced through the
  registrations in `ObservabilityRegistration`; do not add vendor SDKs.
- A new error type that the server raises (like `BadHttpRequestException`) needs a mapping in `ApiExceptionHandler`
  and a test; otherwise it becomes a 500.

## Side effects and e-mail (ADR-0019)

- Handlers never call external services directly. Enqueue a message with `Outbox.Enqueue(...)` and save once; the
  dispatcher delivers it after commit.
- Message handlers (`IOutboxMessageHandler<T>`) must be idempotent: delivery is at least once. Use `context.MessageId`.
- Message type names passed to `AddOutboxMessage` are stored in the database: never rename them.
- Never log message payloads or e-mail bodies; they can contain links and one-time codes.
- Data with a lifetime (tokens, delivered messages) is removed by `HousekeepingCleaner` (ADR-0026). A new table that
  grows without bound gets an index on its expiry column and a batched delete there, not a new scheduler.

## File storage (ADR-0030)

- File content goes through `IFileStorage`; never write files from handlers or put content in the database.
- Storage keys are generated by the application (the file's id); never build a key or path from user input.
- Access to content is decided by the tenant-owned `StoredFile` row: always load it through `IAppDbContext` first.
- Upload: write content, then save once; delete the content again if the save fails. Delete: remove the row and
  enqueue `DeleteFileContentMessage` in the same save.
- Downloads are always attachments. Do not serve content inline, widen the content type allowlist by default, or
  raise the body limit of any endpoint other than the upload without an ADR.

## Audit log (ADR-0028)

- A use case that changes security-relevant state (roles, assignments, invitations, tenants, and anything like them
  added later) calls `AuditLog.Record(...)` before its single `SaveChangesAsync`. Record only real changes.
- Action names in `AuditActions` are a public contract: never rename them.
- Descriptions may name roles, permissions and user ids; never e-mail addresses, tokens, secrets or request bodies.
- The audit log is append-only: never change or delete entries; only housekeeping removes them after the retention.

## Tenancy (ADR-0010)

- Data that belongs to a customer implements `ITenantOwned`. The query filter, foreign key, index and
  write checks are applied automatically; do not filter by tenant in handlers and never set `TenantId` yourself.
- Unique constraints on tenant-owned data start with `TenantId` (`HasIndex(x => new { x.TenantId, x.Name }).IsUnique()`).
- Do not call `IgnoreQueryFilters()` outside a reviewed path. The reviewed paths are invitation acceptance by token hash
  (`AcceptInvitationHandler`, ADR-0027), housekeeping (ADR-0026) and the
  sign-in membership check (`AccountQueries.IsTenantMemberAsync`, ADR-0025), which names the tenant explicitly.
- Raw SQL on tenant-owned tables belongs in Infrastructure, takes the tenant id as a parameter, and needs a cross-tenant test.
- Every new tenant-owned entity needs isolation tests like `TenantIsolationTests` (reads, writes, bulk operations).

## Tests

- New endpoints: integration tests for success, validation failure, not found or conflict where relevant,
  and authorization denial.
- Validators and domain methods with logic: unit tests.
- Security-relevant fixes (authentication, authorization, tenancy): a regression test that fails before the fix.
- Integration tests use `ApiFactory` (`tests/Boilerplate.IntegrationTests/Support`) and pass
  `TestContext.Current.CancellationToken` to async calls.
- Authenticated requests use `factory.CreateAuthenticatedClient(TestTokens.Create(...))`. Every new endpoint gets a
  test that it rejects anonymous callers (401) in addition to its behaviour tests.
- Every permission-protected endpoint gets tests for: missing permission (403), granted permission (allowed), and
  the permission granted only in another tenant (403). `AuthorizationData` creates roles and assignments.
- Persistence tests get real PostgreSQL from the assembly-wide `PostgresFixture` and build services through
  `PersistenceServices` (the real `AddInfrastructure`). Isolate data by creating new tenants per test instead of resetting the database.
- Tests must not depend on execution order, wall-clock time or external networks (Testcontainers excepted).
- Test method names: `Method_or_behaviour_condition_expected_result` with underscores (allowed in `tests/` only).

## Documentation

- Update `README.md`, `docs/architecture.md` or feature guides when a convention or command changes.
- Accepted ADRs are immutable; supersede them with a new ADR. Update the index in `docs/adr/README.md`.

## Changing existing code

1. Read the relevant tests and ADRs first.
2. Before refactoring untested code, add a test that captures its current behaviour.
3. Keep changes scoped: no reformatting, renaming or tidying of unrelated code in the same change.
4. Do not change public contracts (routes, request and response shapes, ProblemDetails `type` values)
   without an explicit versioning decision.
5. Do not weaken validation, authorization or tests to make a build pass.
6. Implement only the milestone or task that was approved; stop and ask before starting the next one.
