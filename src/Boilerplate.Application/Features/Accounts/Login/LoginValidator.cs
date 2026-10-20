using FluentValidation;

namespace Boilerplate.Application.Features.Accounts.Login;

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(request => request.Email).NotEmpty().MaximumLength(AccountRules.EmailMaxLength);
        RuleFor(request => request.Password).ExistingPassword();
        RuleFor(request => request.TenantId).NotEqual(Guid.Empty);
        RuleFor(request => request.TwoFactorCode!).AuthenticatorCode().When(request => request.TwoFactorCode is not null);
        RuleFor(request => request.RecoveryCode!).RecoveryCode().When(request => request.RecoveryCode is not null);
        RuleFor(request => request)
            .Must(request => request.TwoFactorCode is null || request.RecoveryCode is null)
            .WithName(nameof(LoginRequest.RecoveryCode))
            .WithMessage("Send either a two-factor code or a recovery code, not both.");
    }
}
