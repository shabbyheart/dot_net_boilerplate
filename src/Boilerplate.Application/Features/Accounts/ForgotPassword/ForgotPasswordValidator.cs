using FluentValidation;

namespace Boilerplate.Application.Features.Accounts.ForgotPassword;

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        RuleFor(request => request.Email).ValidEmail();
    }
}
