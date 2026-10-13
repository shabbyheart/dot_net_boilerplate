using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// Issues tokens the test API accepts (ADR-0016). The signing key is random per test run, so nothing secret is committed.
/// </summary>
internal static class TestTokens
{
    public const string Issuer = "https://tests.boilerplate.local";
    public const string Audience = "boilerplate-api";
    public const string DefaultSubject = "user-1";

    private static readonly byte[] SigningKey = RandomNumberGenerator.GetBytes(32);

    /// <summary>The settings that make the API accept these tokens.</summary>
    public static IReadOnlyDictionary<string, string?> ApiSettings { get; } = new Dictionary<string, string?>
    {
        ["Authentication:Schemes:Bearer:ValidIssuer"] = Issuer,
        ["Authentication:Schemes:Bearer:ValidAudiences:0"] = Audience,
        ["Authentication:Schemes:Bearer:SigningKeys:0:Issuer"] = Issuer,
        ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] = Convert.ToBase64String(SigningKey),
    };

    public static string Create(
        string? subject = DefaultSubject,
        Guid? tenantId = null,
        string? tenantClaim = null,
        DateTime? expires = null,
        string issuer = Issuer,
        string audience = Audience,
        byte[]? signingKey = null)
    {
        var claims = new Dictionary<string, object>();
        if (subject is not null)
        {
            claims[JwtRegisteredClaimNames.Sub] = subject;
        }

        var tenant = tenantClaim ?? tenantId?.ToString();
        if (tenant is not null)
        {
            claims["tenant_id"] = tenant;
        }

        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(10);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = expiresAt.AddMinutes(-15),
            NotBefore = expiresAt.AddMinutes(-15),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(signingKey ?? SigningKey), SecurityAlgorithms.HmacSha256),
        });
    }
}
