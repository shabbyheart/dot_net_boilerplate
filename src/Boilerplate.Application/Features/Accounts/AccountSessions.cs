using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Security;
using Boilerplate.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Accounts;

/// <summary>
/// Access and refresh token pairs for signed-in users (ADR-0025). Changes are committed by the calling use case.
/// </summary>
public sealed class AccountSessions(
    IAppDbContext db,
    IAccessTokenIssuer accessTokenIssuer,
    AccountSettings settings,
    TimeProvider timeProvider)
{
    /// <summary>Starts a new refresh token family for a sign-in.</summary>
    public TokenResponse Start(string userId, Guid? tenantId)
    {
        var refreshToken = SecureToken.Create();
        var stored = RefreshToken.Issue(
            userId, tenantId, SecureToken.Hash(refreshToken), timeProvider.GetUtcNow(), settings.RefreshTokenLifetime);
        db.RefreshTokens.Add(stored);

        return Response(stored, refreshToken);
    }

    /// <summary>Uses an active refresh token: it is replaced by a new one in the same family.</summary>
    public TokenResponse Rotate(RefreshToken current)
    {
        ArgumentNullException.ThrowIfNull(current);

        var refreshToken = SecureToken.Create();
        var next = current.Rotate(SecureToken.Hash(refreshToken), timeProvider.GetUtcNow(), settings.RefreshTokenLifetime);
        db.RefreshTokens.Add(next);

        return Response(next, refreshToken);
    }

    public async Task RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
        Revoke(await db.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken));

    /// <summary>Signs the user out everywhere, for example after a password reset.</summary>
    public async Task RevokeAllAsync(string userId, CancellationToken cancellationToken) =>
        Revoke(await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken));

    private void Revoke(List<RefreshToken> tokens)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var token in tokens)
        {
            token.Revoke(now);
        }
    }

    private TokenResponse Response(RefreshToken stored, string refreshToken)
    {
        var accessToken = accessTokenIssuer.Issue(stored.UserId, stored.TenantId);
        return new TokenResponse(accessToken.Value, "Bearer", accessToken.ExpiresAt, refreshToken, stored.ExpiresAt);
    }
}
