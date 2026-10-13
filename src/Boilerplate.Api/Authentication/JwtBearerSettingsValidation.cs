using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Boilerplate.Api.Authentication;

/// <summary>
/// Fails startup when the bearer token settings cannot validate tokens safely (ADR-0007, ADR-0016):
/// an audience is required, and either an Authority (keys from its metadata) or an issuer with signing keys.
/// </summary>
internal sealed class JwtBearerSettingsValidation : IValidateOptions<JwtBearerOptions>
{
    private const string Section = "Authentication:Schemes:Bearer";

    public ValidateOptionsResult Validate(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return ValidateOptionsResult.Skip;
        }

        var parameters = options.TokenValidationParameters;
        var failures = new List<string>();

        // Blank values count as missing: configuration binds an empty environment variable as "".
        var hasAudience = !string.IsNullOrWhiteSpace(options.Audience)
            || !string.IsNullOrWhiteSpace(parameters.ValidAudience)
            || parameters.ValidAudiences?.Any(audience => !string.IsNullOrWhiteSpace(audience)) == true;
        if (!hasAudience)
        {
            failures.Add($"{Section}:ValidAudiences is required.");
        }

        var hasAuthority = !string.IsNullOrWhiteSpace(options.Authority)
            || !string.IsNullOrWhiteSpace(options.MetadataAddress);
        var hasIssuer = !string.IsNullOrWhiteSpace(parameters.ValidIssuer)
            || parameters.ValidIssuers?.Any(issuer => !string.IsNullOrWhiteSpace(issuer)) == true;
        var hasSigningKeys = parameters.IssuerSigningKey is not null || parameters.IssuerSigningKeys?.Any() == true;
        if (!hasAuthority && !(hasIssuer && hasSigningKeys))
        {
            failures.Add($"{Section} needs either Authority, or ValidIssuer together with SigningKeys.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
