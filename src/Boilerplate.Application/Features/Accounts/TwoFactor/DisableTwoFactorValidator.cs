using FluentValidation;

namespace Boilerplate.Application.Features.Accounts.TwoFactor;

public sealed class DisableTwoFactorValidator : AbstractValidator<DisableTwoFactorRequest>
{
    public DisableTwoFactorValidator()
    {
        RuleFor(request => request.Password).ExistingPassword();
        RuleFor(request => request.Code!).AuthenticatorCode().When(request => request.Code is not null);
        RuleFor(request => request.RecoveryCode!).RecoveryCode().When(request => request.RecoveryCode is not null);
        RuleFor(request => request)
            .Must(request => (request.Code is null) != (request.RecoveryCode is null))
            .WithName(nameof(DisableTwoFactorRequest.Code))
            .WithMessage("Send either a code from your authenticator app or a recovery code.");
    }
}
