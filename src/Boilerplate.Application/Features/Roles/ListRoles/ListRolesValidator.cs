using Boilerplate.Application.Common.Pagination;
using FluentValidation;

namespace Boilerplate.Application.Features.Roles.ListRoles;

public sealed class ListRolesValidator : AbstractValidator<ListRolesRequest>
{
    public ListRolesValidator()
    {
        Include(new PageRequestValidator());
    }
}
