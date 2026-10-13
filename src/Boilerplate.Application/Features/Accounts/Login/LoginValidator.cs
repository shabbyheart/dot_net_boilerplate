using FluentValidation;

namespace Boilerplate.Application.Features.Accounts.Login;

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(request => request.Email).NotEmpty().MaximumLength(AccountRules.EmailMaxLength);
        RuleFor(request => request.Password).ExistingPassword();
        RuleFor(request => request.TenantId).NotEqual(Guid.Empty);
    }
}
