using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Common.Pagination;

public static class PaginationExtensions
{
    /// <summary>
    /// Counts and reads one page of a query (ADR-0021). Order the query before projecting and paging, with a
    /// deterministic order that ends with the id, so pages neither overlap nor skip rows.
    /// </summary>
    public static async Task<PagedResponse<T>> ToPagedResponseAsync<T>(
        this IQueryable<T> query,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<T>(items, page.Page, page.PageSize, totalCount);
    }
}
