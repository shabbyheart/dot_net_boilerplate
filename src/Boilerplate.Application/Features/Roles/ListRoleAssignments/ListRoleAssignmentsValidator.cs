using Boilerplate.Application.Common.Pagination;
using FluentValidation;

namespace Boilerplate.Application.Features.Roles.ListRoleAssignments;

public sealed class ListRoleAssignmentsValidator : AbstractValidator<ListRoleAssignmentsRequest>
{
    public ListRoleAssignmentsValidator()
    {
        Include(new PageRequestValidator());
    }
}
