# ADR-0013: OpenAPI document and API reference UI

- Status: Accepted
- Date: 2026-10-13

## Context

Clients and developers need a machine-readable API description and a way to browse it while developing.
The previous boilerplate used NSwag and always exposed ReDoc, including in production.

## Alternatives considered

- **Document generation:** Swashbuckle, NSwag, or **the built-in `Microsoft.AspNetCore.OpenApi`**
  (Microsoft's supported generator since .NET 9).
- **Reference UI:** none (document only), Swagger UI, ReDoc, or **Scalar**.

## Decision

- `Microsoft.AspNetCore.OpenApi` (MIT) generates the document, served at `/openapi/v1.json`.
- `Scalar.AspNetCore` (MIT) serves an interactive reference at `/scalar`.
- Both are mapped **only in the Development environment**. Other environments return 404 for these paths.
- Operational endpoints (health checks) are not part of the API document.

## Consequences

- No third-party generator to keep aligned with ASP.NET Core releases.
- One additional UI package. It is only mapped in Development, but it ships in the build output.
- Exposing documentation in other environments later requires a superseding ADR, including how access is protected.
