using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Application.Features.Accounts;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Boilerplate.Infrastructure.Accounts;

/// <summary>Signs access tokens for local sign-in (ADR-0025). The API validates them like any other bearer token (ADR-0016).</summary>
internal sealed class JwtAccessTokenIssuer(IOptions<AccessTokenOptions> options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(string userId, Guid? tenantId)
    {
        var settings = options.Value;

        // Token times have whole-second precision; the reported expiry matches the token.
        var now = DateTimeOffset.FromUnixTimeSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds());
        var expiresAt = now + settings.Lifetime;

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = userId,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
        };
        if (tenantId is { } tenant)
        {
            claims[TenantClaim.Type] = tenant.ToString();
        }

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = settings.CreateSigningCredentials(),
        });

        return new AccessToken(token, expiresAt);
    }
}
