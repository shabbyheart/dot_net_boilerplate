using Boilerplate.Application.Features.Accounts;
using Boilerplate.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Boilerplate.Api.Registration;

/// <summary>
/// When local accounts are enabled, the bearer scheme also accepts the API's own tokens (ADR-0025): their issuer,
/// audience and signing keys are added to whatever <c>Authentication:Schemes:Bearer</c> configures (ADR-0016).
/// In the Registration namespace because it reads Infrastructure's token settings (ADR-0001).
/// </summary>
internal static class LocalAccountTokenRegistration
{
    public static IServiceCollection AddLocalAccountTokens(this IServiceCollection services)
    {
        // Runs before the bearer settings are validated, so local tokens alone satisfy the startup check.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure<IOptions<AccountSettings>, IOptions<AccessTokenOptions>>((bearer, accounts, tokens) =>
            {
                if (!accounts.Value.Enabled)
                {
                    return;
                }

                var local = tokens.Value;
                var parameters = bearer.TokenValidationParameters;
                parameters.ValidIssuers = [.. parameters.ValidIssuers ?? [], local.Issuer!];
                parameters.ValidAudiences = [.. parameters.ValidAudiences ?? [], local.Audience!];
                parameters.IssuerSigningKeys = [.. parameters.IssuerSigningKeys ?? [], .. local.CreateValidationKeys()];
            });

        return services;
    }
}
