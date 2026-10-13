using FluentValidation;

namespace Boilerplate.Application.Features.Accounts.Register;

public sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(request => request.Email).ValidEmail();
        RuleFor(request => request.Password).NewPassword();
    }
}
