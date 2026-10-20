using Boilerplate.Application.Common.Pagination;
using FluentValidation;

namespace Boilerplate.Application.Features.Members.ListMembers;

public sealed class ListMembersValidator : AbstractValidator<ListMembersRequest>
{
    public ListMembersValidator()
    {
        Include(new PageRequestValidator());
    }
}
