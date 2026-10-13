# ADR-0006: Error handling and the ProblemDetails contract

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate serialised its own error JSON into exception messages, used a custom error shape,
had swapped arguments in its error helper, and returned internal exception types and messages to clients
on unexpected failures. Clients need one predictable error format, and internals must never leak.

## Alternatives considered

1. **Result types everywhere.** Handlers return `Result<T>`; endpoints translate. Explicit, but every handler
   signature and test carries the wrapper.
2. **A small set of typed exceptions mapped centrally to ProblemDetails.**
3. **Custom error envelope** (the previous approach).

## Decision

Option 2.

- All error responses use **RFC 9457 ProblemDetails** (`application/problem+json`), written by the
  framework's `IProblemDetailsService` (`AddProblemDetails()`).
- `type` and `title` use the framework defaults for the status code (links to RFC 9110 sections), so they
  are stable without a custom catalogue. Every response includes the `traceId` extension, which matches the logs.
- One `IExceptionHandler` (`Boilerplate.Api.ErrorHandling.ApiExceptionHandler`) maps exceptions:

  | Exception | Status | `detail` | Log level |
  |---|---|---|---|
  | `NotFoundException` (Application) | 404 | The exception message (written by our code, safe to show) | Information, no stack trace |
  | Any other exception | 500 | None. Never the exception message or type | Error, with exception |

- Further mappings (validation 400, conflict 409, forbidden 403, domain rule violations) are added by the
  milestone that first needs them, in this handler, together with their tests.
- Error responses without a body (for example an unknown route) are converted to ProblemDetails by
  `UseStatusCodePages()`.
- Exceptions carry data (resource name, key), never pre-serialised JSON.

## Consequences

- Handlers stay simple: throw a typed exception and let the edge translate it.
- Exceptions cost more than return values, which is irrelevant at normal API failure rates.
  Do not use exceptions for expected outcomes that are not failures (see AGENTS.md).
- Since .NET 10, exceptions handled by an `IExceptionHandler` are not logged by the exception handler
  middleware, so `ApiExceptionHandler` is responsible for logging them.
