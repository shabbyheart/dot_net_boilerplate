# ADR-0023: Tracing and metrics

- Status: Accepted. Supersedes the deferred tracing and metrics part of ADR-0008 (logging stays as decided there).
- Date: 2026-10-13

## Context

ADR-0008 settled logging (Serilog behind `ILogger<T>`) and deferred tracing and metrics. Operating the API needs
request traces across HTTP and database calls, basic metrics, and a correlation id that clients, logs and traces share.
The observability backend (Grafana, Azure Monitor, Datadog, ...) is not chosen yet.

## Alternatives considered

- Vendor SDKs (Application Insights, Datadog) or **OpenTelemetry with the OTLP exporter** (vendor-neutral).
- Export always on, or **export only when an endpoint is configured**.

## Decision

- OpenTelemetry (Apache-2.0) with:
  - traces: ASP.NET Core server requests, outgoing `HttpClient` calls, PostgreSQL commands (`Npgsql.OpenTelemetry`);
  - metrics: ASP.NET Core, `HttpClient`, .NET runtime.
- Export uses OTLP and is enabled only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (the standard OpenTelemetry
  environment variables also control protocol, headers and `OTEL_SERVICE_NAME`; the default service name is
  `boilerplate-api`). Without it, instrumentation still produces trace ids but exports nothing.
- Logs remain Serilog (ADR-0008); log events carry the current trace and span ids.
- The `traceId` in ProblemDetails is the request's W3C trace context (`00-<trace id>-<span id>-01`); its trace id
  leads from a client report to the trace and the logs.
- Health endpoints are not traced (they would dominate trace volume).

**Packages:** `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`,
`OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Instrumentation.Runtime`,
`OpenTelemetry.Exporter.OpenTelemetryProtocol`, `Npgsql.OpenTelemetry`; tests add `OpenTelemetry.Exporter.InMemory`.

## Consequences

- Any OTLP-capable backend works by setting environment variables.
- A small runtime cost for instrumentation, even with export off.
- Log export through OpenTelemetry is not configured; logs go to the console (container logs).
