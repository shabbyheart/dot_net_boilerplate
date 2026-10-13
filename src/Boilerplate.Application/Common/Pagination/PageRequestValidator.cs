using FluentValidation;

namespace Boilerplate.Application.Common.Pagination;

/// <summary>Page limits shared by every list request (ADR-0021). Use with <c>Include(new PageRequestValidator())</c>.</summary>
public sealed class PageRequestValidator : AbstractValidator<PageRequest>
{
    public PageRequestValidator()
    {
        RuleFor(request => request.Page).GreaterThanOrEqualTo(1);
        RuleFor(request => request.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize);
    }
}
