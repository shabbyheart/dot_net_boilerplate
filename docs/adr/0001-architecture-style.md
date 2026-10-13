# ADR-0001: Architecture style and project dependencies

- Status: Accepted
- Date: 2026-10-13

## Context

The boilerplate is the starting point for several backend applications. The audit of the previous
boilerplate found the dependency direction reversed: the application layer referenced Infrastructure,
business rules lived in Infrastructure services, and Domain held infrastructure details such as
template file names. Folder conventions alone did not prevent this. People and AI coding agents both
need boundaries that are obvious and mechanically enforced.

## Alternatives considered

1. **Single project with feature folders.** Simplest. Cannot stop `HttpContext` or EF Core from leaking
   into business logic, because everything can see everything.
2. **Layered N-tier where Application depends on Infrastructure** (the previous boilerplate). Business
   logic cannot be built or tested without every infrastructure package.
3. **Four projects with Clean Architecture dependency direction and vertical feature slices.**
4. **Full DDD/hexagonal design with one assembly per module.** More isolation than current needs justify.

## Decision

Option 3:

- `Boilerplate.Domain`: no project or package references.
- `Boilerplate.Application`: references Domain. Contains use cases as vertical slices
  (`Features/<Area>/<UseCase>/`) and the ports they need.
- `Boilerplate.Infrastructure`: references Application only (Domain arrives transitively).
- `Boilerplate.Api`: references Application and Infrastructure. Uses Infrastructure only in the
  composition root (`Program.cs`, `Boilerplate.Api.Registration`).

Application may reference only allowlisted packages. Initially the allowlist is abstractions-only
`Microsoft.Extensions.*.Abstractions` packages; later ADRs extend it explicitly (for example ADR-0005 for EF Core).

Rules are enforced by `tests/Boilerplate.ArchitectureTests`, checking both declared references and compiled usage.

## Consequences

- The compiler and the architecture tests enforce the direction; reviews do not have to.
- A use case lives in one folder, so a change is usually local to it.
- Four projects add some navigation overhead, which a tiny application would not need.
- Api references Infrastructure, so endpoint code *could* use it; an architecture test restricts this.
- Adding a project, project reference or Application package requires a new ADR and a test update.
