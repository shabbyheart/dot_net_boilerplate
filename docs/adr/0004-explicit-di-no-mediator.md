# ADR-0004: Explicit dependency injection; no mediator library

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate registered services by scanning for marker interfaces and registering each
class against `GetInterfaces().FirstOrDefault()`. That made registrations depend on reflection order
and put DI concerns into every interface. It also used MediatR to send commands from controllers to
handlers, but used no pipeline behaviours, so the library added indirection without its main benefit.
MediatR versions 13 and later require a commercial licence for many uses.

## Alternatives considered

- **Registration:** marker-interface scanning; Scrutor convention scanning; **explicit registration per layer and feature**.
- **Dispatch:** MediatR; a hand-written mediator; **plain handler classes injected into endpoints**.

## Decision

- Each layer exposes one registration extension method (`AddApplication()`, `AddInfrastructure(configuration)`),
  and each feature area may add its own (`AddRolesFeature()`). Every service is registered explicitly with a deliberate lifetime.
- The host validates the container at startup (`ValidateOnBuild` and `ValidateScopes`) in all environments,
  starting with M1 when the first services are registered.
- A use case is a plain handler class with a `HandleAsync(request, cancellationToken)` method,
  registered explicitly and injected into its endpoint. No mediator library.
- Cross-cutting concerns use ASP.NET Core mechanisms: endpoint filters for validation, middleware and
  OpenTelemetry for logging and tracing, and the single commit point for transactions.

## Consequences

- "Go to definition" from an endpoint reaches the handler directly; registrations can be found with text search.
- One extra registration line per handler.
- There is no generic handler pipeline. If many cross-cutting behaviours become necessary later,
  introducing a mediator is a mechanical change, made through a superseding ADR.
