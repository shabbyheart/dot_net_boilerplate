using Boilerplate.Api.Authorization;
using Boilerplate.Application.Common.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Boilerplate.Api.Authentication;

/// <summary>JWT bearer authentication and the deny-by-default authorization policy (ADR-0016).</summary>
internal static class AuthenticationRegistration
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        // Settings come from configuration: Authentication:Schemes:Bearer (Authority, ValidIssuer, ValidAudiences, SigningKeys).
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Defence in depth: the framework validates these by default, but this keeps every check on even if defaults
        // or configuration change. With an Authority, its metadata supplies the issuer and signing keys.
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.MapInboundClaims = false;

            var parameters = options.TokenValidationParameters;
            parameters.ValidateIssuer = true;
            parameters.ValidateAudience = true;
            parameters.ValidateLifetime = true;
            parameters.ValidateIssuerSigningKey = true;
            parameters.ClockSkew = TimeSpan.FromMinutes(1);
            parameters.NameClaimType = JwtRegisteredClaimNames.Sub;
        });

        services.AddSingleton<IValidateOptions<JwtBearerOptions>, JwtBearerSettingsValidation>();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).ValidateOnStart();

        // Every endpoint requires an authenticated user with a subject, unless it explicitly allows anonymous access.
        // The default policy (used by [RequirePermission] and any other [Authorize]) is the same baseline (ADR-0017).
        var authenticatedUser = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(JwtRegisteredClaimNames.Sub)
            .Build();
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(authenticatedUser)
            .SetDefaultPolicy(authenticatedUser);

        // Permission and platform administrator checks query the database per request, so the handlers are scoped
        // like the database context.
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, PlatformAdministratorAuthorizationHandler>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, ClaimsCurrentUser>();

        return services;
    }
}
