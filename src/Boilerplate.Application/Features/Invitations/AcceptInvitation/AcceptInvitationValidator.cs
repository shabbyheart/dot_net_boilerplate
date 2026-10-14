using Boilerplate.Application.Common.Security;
using FluentValidation;

namespace Boilerplate.Application.Features.Invitations.AcceptInvitation;

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationValidator()
    {
        RuleFor(request => request.Token).NotEmpty().MaximumLength(SecureToken.MaxLength);
    }
}
