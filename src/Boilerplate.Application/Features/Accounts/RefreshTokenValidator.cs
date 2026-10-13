using FluentValidation;

namespace Boilerplate.Application.Features.Accounts;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenValidator()
    {
        RuleFor(request => request.RefreshToken).ValidToken();
    }
}
