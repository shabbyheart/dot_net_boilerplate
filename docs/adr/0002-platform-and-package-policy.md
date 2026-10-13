# ADR-0002: Platform, SDK pinning, Central Package Management and package policy

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate had about 45 packages in one project, including unused, deprecated and
vulnerable ones, versions spread across project files, and a `global.json` that pinned an SDK major
different from the target framework. Builds must be reproducible, and every dependency must be deliberate.

## Alternatives considered

- **Target framework:** .NET 9 (STS, support ending) or **.NET 10 (LTS)**.
- **Versions:** per-project `Version` attributes, or **Central Package Management** (`Directory.Packages.props`).
- **Analyzers:** StyleCop + Roslynator, or **the built-in .NET analyzers**, with or without warnings as errors.

## Decision

- Target `net10.0`. `global.json` pins SDK `10.0.100` with `rollForward: latestFeature`, so any newer
  10.0 feature band works but other majors don't.
- All package versions live in `Directory.Packages.props`; project files never contain versions.
- Shared build settings live in `Directory.Build.props`: nullable reference types, implicit usings,
  `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, `TreatWarningsAsErrors=true`
  (locally and in CI, so both behave the same). Rule severities and documented exceptions live in `.editorconfig`.
- **Package policy:**
  - A new package needs a line in the ADR for the milestone that introduces it: purpose, licence, and
    alternatives considered (including "no package").
  - Check the licence before adopting a package or a new major version; several popular .NET libraries have moved to commercial licences.
  - **Banned** (enforced by `PackagePolicyTests`):
    - `MediatR`: see ADR-0004.
    - `Ardalis.*`: the owner decided against the Specification pattern and generic repositories.
      Queries are written in handlers; the persistence approach is recorded in ADR-0005.

## Consequences

- One place to read and upgrade versions; fewer accidental version drifts.
- Warnings fail the build, which is stricter while learning but keeps the codebase clean.
- No StyleCop. It can be added later through a new ADR if the team wants stricter style rules.
- .NET 10 LTS is supported until November 2028 (check Microsoft's support policy for exact dates).
