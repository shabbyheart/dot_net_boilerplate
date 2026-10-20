using FluentValidation;

namespace Boilerplate.Application.Features.Accounts.TwoFactor;

public sealed class EnableTwoFactorValidator : AbstractValidator<EnableTwoFactorRequest>
{
    public EnableTwoFactorValidator()
    {
        RuleFor(request => request.Password).ExistingPassword();
        RuleFor(request => request.Code).AuthenticatorCode();
    }
}
