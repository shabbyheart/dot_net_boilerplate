using Boilerplate.Application.Common.Pagination;
using FluentValidation;

namespace Boilerplate.Application.Features.Tenants.ListTenants;

public sealed class ListTenantsValidator : AbstractValidator<ListTenantsRequest>
{
    public ListTenantsValidator()
    {
        Include(new PageRequestValidator());
    }
}
