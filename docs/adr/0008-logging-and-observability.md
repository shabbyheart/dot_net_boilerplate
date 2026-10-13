# ADR-0008: Logging and observability

- Status: Accepted (logging). Tracing and metrics will be decided in a superseding ADR before M10.
- Date: 2026-10-13

## Context

The previous boilerplate used static logging helpers, two separately built logger configurations, a filter
that dropped every warning, request logging that wrote authorization headers and bodies (including login
passwords), and a response logger that always logged an empty body. Logs must be structured, safe and
correlated with error responses.

## Alternatives considered

- **API used by code:** static `Serilog.Log`, or **`ILogger<T>` from Microsoft.Extensions.Logging**.
- **Provider:** the built-in console logger, or **Serilog as the provider** behind `ILogger<T>`.
- **Request logging:** custom middleware, ASP.NET Core `HttpLogging`, or **Serilog request logging**
  (one summary event per request, no headers or bodies).

## Decision

- Application code logs through `ILogger<T>` only. The static `Log` is used solely by `Program.cs`
  to report startup failures before the host's logging exists.
- Serilog is the logging provider, configured once in `Program.cs` (`AddSerilog`):
  - minimum levels and overrides come from the `Serilog` configuration section;
  - sinks are set in code: readable console output in Development, **compact JSON on the console**
    everywhere else (collected by the container platform);
  - `preserveStaticLogger: true` keeps the startup logger separate, so several hosts can run in one
    process (integration tests).
- `UseSerilogRequestLogging()` writes one event per request (method, path, status, elapsed time).
  It never logs headers or bodies.
- Error responses carry `traceId` (ADR-0006), which also appears on log events, for correlation.
- Rules for log content are in AGENTS.md: message templates, no secrets, no request bodies.

**Package:** `Serilog.AspNetCore` (Apache-2.0). It brings the console sink, compact JSON formatter and
configuration support in one maintained package. Alternative considered: the built-in console logger
with its JSON formatter, which has no request summary logging and less flexible level overrides.

## Consequences

- One logging configuration; no static helpers in application code.
- Container logs are machine-readable; local logs stay readable.
- Distributed tracing and metrics (OpenTelemetry) are deferred to M10.
