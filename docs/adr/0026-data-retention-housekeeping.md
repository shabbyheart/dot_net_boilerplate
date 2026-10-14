# ADR-0026: Data retention and housekeeping

- Status: Accepted
- Date: 2026-10-14

## Context

ADR-0019 keeps delivered outbox rows (without payload) "until a retention job is added", and ADR-0025 leaves expired
refresh and one-time tokens in place "until a cleanup job exists". These tables grow with every e-mail, sign-in and
refresh, and old token rows have no use once they expire. Nothing removes them today.

## Alternatives considered

- **Scheduler:** a job library (Hangfire, Quartz.NET), a database extension (`pg_cron`), an external scheduler
  (Kubernetes CronJob running a command), or **a hosted service on a timer**, like the outbox dispatcher.
- **Coordination across instances:** a leader lock (advisory lock or lease row), or **none: deleting is idempotent**,
  so instances running the same cleanup at once only find fewer rows.
- **Deleting:** one statement per table, or **bounded batches** repeated until nothing is left.
- **Timing:** delete at expiry, or **keep rows for a configurable retention after expiry or delivery**.

## Decision

- `HousekeepingService` (hosted service, Infrastructure) runs `HousekeepingCleaner` at startup and then every
  `Housekeeping:Interval` (default 1 hour). Failures are logged and retried on the next tick; they never stop the host.
- Each round deletes, in batches of `Housekeeping:BatchSize` (default 1000) rows per statement:
  - refresh tokens and one-time tokens whose `expires_at` is older than `Housekeeping:ExpiredTokenRetention`
    (default 7 days). A replaced or revoked refresh token is kept until it expires, so presenting it again still
    revokes its family (ADR-0025);
  - delivered outbox messages whose `processed_at` is older than `Housekeeping:ProcessedOutboxRetention`
    (default 7 days).
- **Dead-lettered and pending outbox messages are never deleted.** Dead letters need an operator (ADR-0019).
- No coordination between instances. `Housekeeping:Enabled` (default on) turns it off per instance.
- Indexes on `expires_at` of both token tables and a partial index on delivered outbox messages keep the deletes
  from scanning whole tables.
- A round logs how many rows it deleted (counts only, never row content).

## Consequences

- The token and outbox tables stay bounded by traffic within the retention window instead of growing forever.
- No new dependency; the behaviour follows the outbox dispatcher, so there is one pattern for background work.
- Every instance runs the cleanup unless disabled; this costs a few cheap index lookups per hour per instance.
- There is no general job scheduler. Work that needs calendar schedules, history or a dashboard requires a new decision.
- Dead-lettered outbox messages still accumulate until an operator handles them; their payloads (which can contain
  links) stay until then.
