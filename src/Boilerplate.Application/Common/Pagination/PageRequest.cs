namespace Boilerplate.Application.Common.Pagination;

/// <summary>
/// Paging parameters of list requests (ADR-0021). List requests derive from this type and include
/// <see cref="PageRequestValidator"/> in their validator.
/// </summary>
public record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Items per page, between 1 and <see cref="MaxPageSize"/>.</summary>
    public int PageSize { get; init; } = DefaultPageSize;
}
