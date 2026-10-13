# ADR-0012: IDs, time and soft delete

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate used a third-party sequential GUID library, read the system clock directly (so expiry
logic was untestable), and implemented soft delete without global filters, which let deleted role permissions
stay effective.

## Alternatives considered

- **IDs:** integer identity, random GUID v4, library sequential GUIDs, **GUID v7** (`Guid.CreateVersion7()`).
- **Time:** `DateTime.UtcNow`; **`TimeProvider`**; `DateTime` vs **`DateTimeOffset`** values.
- **Soft delete:** on every entity from the start, or **only for features that need it**.

## Decision

- Entity ids are `Guid` values created with `Guid.CreateVersion7()` in the entity (base class `Entity`).
  They are time-ordered (index-friendly), need no database round trip, and are safe to expose in URLs.
- Time comes from the injected `TimeProvider` (`TimeProvider.System` in production, `FakeTimeProvider` in tests).
  Timestamps are `DateTimeOffset` in UTC, stored as `timestamptz`.
- Auditable entities (`IAuditable`) have `CreatedAt` and `UpdatedAt`, set only by `AuditTimestampsInterceptor`.
  `CreatedBy` and `UpdatedBy` arrive with authentication (M5).
- **No soft delete in the foundation.** A feature that needs it adds `ISoftDeletable`, an interceptor and a
  named global query filter through its own ADR, with tests that deleted rows are invisible, including through navigations.

## Consequences

- Ids reveal creation time order. This is acceptable for this foundation; a feature that must hide it uses a separate public identifier.
- Every time-dependent rule is testable without waiting.
- Deleting is permanent unless a feature opts in to soft delete.
