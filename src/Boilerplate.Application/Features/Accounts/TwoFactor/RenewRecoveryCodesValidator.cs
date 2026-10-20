using FluentValidation;

namespace Boilerplate.Application.Features.Accounts.TwoFactor;

public sealed class RenewRecoveryCodesValidator : AbstractValidator<RenewRecoveryCodesRequest>
{
    public RenewRecoveryCodesValidator()
    {
        RuleFor(request => request.Password).ExistingPassword();
        RuleFor(request => request.Code).AuthenticatorCode();
    }
}
