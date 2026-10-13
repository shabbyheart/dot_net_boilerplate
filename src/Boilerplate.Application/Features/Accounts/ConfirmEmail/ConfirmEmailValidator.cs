using FluentValidation;

namespace Boilerplate.Application.Features.Accounts.ConfirmEmail;

public sealed class ConfirmEmailValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailValidator()
    {
        RuleFor(request => request.Token).ValidToken();
    }
}
