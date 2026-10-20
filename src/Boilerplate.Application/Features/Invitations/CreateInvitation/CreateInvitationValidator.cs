using FluentValidation;

namespace Boilerplate.Application.Features.Invitations.CreateInvitation;

public sealed class CreateInvitationValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationValidator()
    {
        RuleFor(request => request.Email).InvitedEmail();
        RuleFor(request => request.RoleId).NotEmpty();
    }
}
