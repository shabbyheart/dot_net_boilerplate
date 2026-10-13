namespace Boilerplate.Application.Common.Pagination;

/// <summary>One page of a list (ADR-0021).</summary>
/// <param name="Items">The items on this page, in the endpoint's documented order.</param>
/// <param name="Page">The 1-based page number.</param>
/// <param name="PageSize">The requested page size.</param>
/// <param name="TotalCount">The number of items across all pages.</param>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
