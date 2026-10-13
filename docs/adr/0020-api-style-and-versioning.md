# ADR-0020: API style and versioning

- Status: Accepted
- Date: 2026-10-13

## Context

Features need a consistent HTTP surface. The previous boilerplate used controllers with a deprecated
versioning package whose version attributes were never applied.

## Alternatives considered

- **Style:** Minimal APIs with endpoint groups, or **MVC controllers**.
- **Versioning:** a versioning library from the start, or **a URL prefix now and a library only when a second version exists**.

## Decision

- **MVC controllers** with `[ApiController]`, in `src/Boilerplate.Api/Controllers`, one controller per feature area.
  Controllers stay thin: they bind the request, call one Application handler, and return the result.
- Routes start with **`/api/v1`**. `Asp.Versioning.Mvc` is added through a new ADR only when a v2 is needed.
- Controllers and their request and response types are `public`, because MVC discovers only public controllers.
- Errors use ProblemDetails (ADR-0006). Validation details (ADR-0014) are decided with the first feature that validates input.

## Consequences

- Familiar structure for teams used to controllers; filters and model binding come from MVC.
- Slightly more ceremony than Minimal APIs.
- Changing the URL versioning scheme later would break clients, so `/api/v1` is a commitment.
