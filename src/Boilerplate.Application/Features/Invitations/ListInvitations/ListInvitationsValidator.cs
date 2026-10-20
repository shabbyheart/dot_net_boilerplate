using Boilerplate.Application.Common.Pagination;
using FluentValidation;

namespace Boilerplate.Application.Features.Invitations.ListInvitations;

public sealed class ListInvitationsValidator : AbstractValidator<ListInvitationsRequest>
{
    public ListInvitationsValidator()
    {
        Include(new PageRequestValidator());
    }
}
