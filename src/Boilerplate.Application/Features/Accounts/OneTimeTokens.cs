using Boilerplate.Application.Common.Persistence;
using Boilerplate.Domain.Accounts;
using Boilerplate.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Accounts;

/// <summary>Issues and redeems e-mailed single-use tokens (ADR-0025). Changes are committed by the calling use case.</summary>
public sealed class OneTimeTokens(IAppDbContext db, TimeProvider timeProvider)
{
    /// <summary>Creates a token and makes the user's earlier unused tokens for the same purpose unusable.</summary>
    /// <returns>The token to send; only its hash is stored.</returns>
    public async Task<string> IssueAsync(
        string userId,
        OneTimeTokenPurpose purpose,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var earlier = await db.OneTimeTokens
            .Where(token => token.UserId == userId && token.Purpose == purpose && token.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in earlier)
        {
            token.Invalidate(now);
        }

        var value = SecureToken.Create();
        db.OneTimeTokens.Add(OneTimeToken.Issue(userId, purpose, SecureToken.Hash(value), now, lifetime));
        return value;
    }

    /// <exception cref="DomainException">The token is unknown, for another purpose, used or expired.</exception>
    public async Task<OneTimeToken> RedeemAsync(string value, OneTimeTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var hash = SecureToken.Hash(value);
        var token = await db.OneTimeTokens
            .SingleOrDefaultAsync(token => token.TokenHash == hash && token.Purpose == purpose, cancellationToken)
            ?? throw new DomainException(OneTimeToken.InvalidMessage);

        token.Redeem(timeProvider.GetUtcNow());
        return token;
    }
}
