# ADR-0024: HTTP hardening and operational behaviour

- Status: Accepted. Assumes TLS terminates at a reverse proxy or load balancer in front of the API; revisit when the
  deployment target (A14) is chosen.
- Date: 2026-10-13

## Context

The previous boilerplate had misspelled security headers, CORS that allowed any origin regardless of
configuration, no HSTS, no rate limiting and no request size policy. This boilerplate so far had none of these
either, plus three operational gaps found in earlier milestones: readiness ignored pending migrations, health probes
and expected database failures produced noisy logs, and Data Protection logged warnings at every start.

## Alternatives considered

- **TLS:** HTTPS in the application (certificates in containers), or **TLS at a proxy** with forwarded headers.
- **Forwarded headers:** trust all (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`), or **trust configured proxies only**.
- **Rate limiting:** a gateway product, or **ASP.NET Core rate limiting middleware**, per user or client IP.
- **Data Protection keys:** persisted (database, blob storage), or **in memory**, because this API uses no cookies
  or protected payloads.

## Decision

**Proxy and TLS**

- The API listens on HTTP; TLS terminates at the proxy. `X-Forwarded-For` and `X-Forwarded-Proto` are honoured only
  from addresses in `ForwardedHeaders:KnownProxies` and networks in `ForwardedHeaders:KnownNetworks` (CIDR);
  by default only loopback is trusted.
- Outside Development, HSTS (`max-age` one year) is sent on HTTPS requests. No HTTPS redirection in the application.

**Security headers** on every response: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
`Referrer-Policy: no-referrer`, and `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`
(not on the Development-only API reference page, which needs scripts).

**CORS:** browsers from origins in `Cors:AllowedOrigins` may call the API (any header, GET/POST/PUT/DELETE,
no credentials: tokens travel in the `Authorization` header). The default list is empty: no cross-origin access.

**Rate limiting:** a fixed window per caller: the token subject when authenticated, otherwise the client IP after
forwarded-header processing. Defaults: 100 requests per 10 seconds (`RateLimiting:PermitLimit`, `RateLimiting:Window`).
Excess requests get 429 ProblemDetails with `Retry-After`. Health endpoints are not limited.
Limits are per instance; a gateway is needed for a global limit across instances.

**Request size:** Kestrel rejects request bodies larger than 1 MB (`Kestrel:Limits:MaxRequestBodySize`) with 413.

**Readiness:** `/health/ready` is unhealthy while the database has pending migrations, so an instance is not sent
traffic before the schema matches the code (migrations still run as a separate step, ADR-0011).

**Log noise**

- Health probes are logged at Debug, not as Information request summaries.
- EF Core's own error events for failed queries and saves are lowered to Debug: the code that handles them logs once
  (the exception handler, the outbox dispatcher), and expected outcomes such as a 409 conflict are not errors.

**Data Protection:** keys are kept in memory (ephemeral). Nothing in the API uses Data Protection today; a feature that
needs it (cookies, antiforgery, protected links) must persist keys and share them across instances first.

## Consequences

- Secure defaults without code changes per feature; operators tune limits, origins and trusted proxies in configuration.
- A misconfigured proxy list means the API sees the proxy's IP (shared rate limit bucket) and `http` as the scheme
  (no HSTS); the README documents the settings.
- Per-instance rate limits scale with the number of instances.
