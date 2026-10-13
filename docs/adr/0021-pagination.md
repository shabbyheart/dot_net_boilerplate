# ADR-0021: Pagination of list endpoints

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate allowed `pageSize = -1` to return every row, with no upper limit, so one request could
load a whole table. List endpoints need one predictable contract that clients can rely on.

## Alternatives considered

- **Style:** offset pagination (`page`, `pageSize`) or keyset/cursor pagination.
- **Too large a page size:** silently clamp, or **reject with 400**.

## Decision

- List endpoints use offset pagination with query parameters `page` (default 1, at least 1) and `pageSize`
  (default 20, between 1 and 100). Values outside these ranges are rejected with 400 validation errors,
  so clients learn the limits instead of receiving silently different pages.
- The response is `PagedResponse<T>`: `items`, `page`, `pageSize`, `totalCount`.
- Every paged query has a deterministic order (ending with the id), so pages do not overlap or skip rows.
- Shared types live in `Application/Common/Pagination` (`PageRequest`, `PagedResponse<T>`, `ToPagedResponseAsync`).

## Consequences

- Simple for clients and tooling.
- Offset pagination gets slower on deep pages and can shift when rows are inserted concurrently. A feature that
  needs very large or real-time lists adopts keyset pagination through its own ADR.
- `totalCount` costs one count query per page.
