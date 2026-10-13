# ADR-0014: Validation

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate relied on FluentValidation's MVC auto-validation (now deprecated), which validated only
model-bound arguments; commands built inside controllers were never validated. Validation errors were logged
as errors and returned in a custom shape.

## Alternatives considered

- **Rules:** DataAnnotations attributes on request types, or **FluentValidation validators** per request.
- **Where validation runs:** inside each handler; FluentValidation's deprecated auto-validation; **an MVC action
  filter that validates every action argument that has a registered validator**.
- **Registration:** assembly scanning, or **explicit registration per feature** (ADR-0004).

## Decision

- Each request that carries input has a `<UseCase>Validator : AbstractValidator<<UseCase>Request>` next to it in
  `Application/Features/<Area>/<UseCase>/`. Validators check shape and limits (required, lengths, ranges).
- Domain invariants stay in the domain (`DomainException`, mapped to 400). Rules that need the database (uniqueness)
  live in the handler (`ConflictException`, mapped to 409).
- `Boilerplate.Api.Validation.ValidationFilter` (an `IAsyncActionFilter`, registered for all controllers) resolves
  `IValidator<T>` for each action argument and, on failure, returns 400 `ValidationProblemDetails` with an `errors`
  dictionary keyed by property name (camelCase, matching the JSON).
- Validators are registered explicitly in the feature's registration method.
- Validation failures are client errors: they are not logged as errors.

**Package:** `FluentValidation` (Apache-2.0) in Application, added to the Application package allowlist.
Not `FluentValidation.AspNetCore` (deprecated) and not `FluentValidation.DependencyInjectionExtensions` (scanning).

## Consequences

- Every bound request with a validator is validated before the handler runs, with one consistent error shape.
- One registration line per validator.
- Validators that need services (for example the database) are possible but discouraged; put such rules in the handler.
