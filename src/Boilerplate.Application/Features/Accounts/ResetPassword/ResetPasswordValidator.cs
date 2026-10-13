using FluentValidation;

namespace Boilerplate.Application.Features.Accounts.ResetPassword;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(request => request.Token).ValidToken();
        RuleFor(request => request.NewPassword).NewPassword();
    }
}
