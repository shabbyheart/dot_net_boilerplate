using Boilerplate.Application.Features.Accounts.ConfirmEmail;
using Boilerplate.Application.Features.Accounts.ForgotPassword;
using Boilerplate.Application.Features.Accounts.Login;
using Boilerplate.Application.Features.Accounts.Logout;
using Boilerplate.Application.Features.Accounts.Refresh;
using Boilerplate.Application.Features.Accounts.Register;
using Boilerplate.Application.Features.Accounts.ResetPassword;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Application.Features.Accounts;

/// <summary>
/// Registers the local account use cases explicitly (ADR-0004, ADR-0025). Infrastructure provides
/// <see cref="IUserAccounts"/>, <see cref="IAccessTokenIssuer"/> and <see cref="AccountSettings"/>.
/// </summary>
public static class AccountsFeature
{
    public static IServiceCollection AddAccountsFeature(this IServiceCollection services)
    {
        services.AddScoped<OneTimeTokens>();
        services.AddScoped<AccountSessions>();

        services.AddScoped<RegisterHandler>();
        services.AddScoped<ConfirmEmailHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();

        services.AddScoped<IValidator<RegisterRequest>, RegisterValidator>();
        services.AddScoped<IValidator<ConfirmEmailRequest>, ConfirmEmailValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginValidator>();
        services.AddScoped<IValidator<RefreshTokenRequest>, RefreshTokenValidator>();
        services.AddScoped<IValidator<ForgotPasswordRequest>, ForgotPasswordValidator>();
        services.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordValidator>();

        return services;
    }
}
