using Boilerplate.Domain.Authorization;
using FluentValidation;

namespace Boilerplate.Application.Features.Invitations;

/// <summary>Validation rules shared by the requests that invite someone.</summary>
public static class InvitationRules
{
    public static IRuleBuilderOptions<T, string> InvitedEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().EmailAddress().MaximumLength(Invitation.EmailMaxLength);
}
