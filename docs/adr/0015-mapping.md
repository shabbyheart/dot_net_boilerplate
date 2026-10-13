# ADR-0015: Mapping

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate used Mapster, including a repository override that projected entities onto themselves and
silently changed query results. AutoMapper now requires a commercial licence. Mapping code is read and changed
often, by people and by agents, so it must be obvious.

## Alternatives considered

- AutoMapper (commercial), Mapster, Mapperly (source-generated), **hand-written mapping**.

## Decision

- Responses are records in the feature folder. Mapping is written by hand:
  - queries project in the database with an expression, for example `ProjectResponse.FromProject`
    (`Expression<Func<Project, ProjectResponse>>`), reused by the handlers of one feature;
  - write handlers map the saved entity with the same expression compiled once, or construct the response directly.
- Requests are never entities; entities are never returned from controllers.
- No mapping library. Adding one requires a superseding ADR.

## Consequences

- Every mapped field is visible in code and covered by the compiler.
- Some repetitive code for large responses.
- Projections select only the needed columns, because EF Core translates the expression to SQL.
