using Boilerplate.Domain.Common;

namespace Boilerplate.Domain.Accounts;

/// <summary>
/// A single-use token sent by e-mail, such as an e-mail confirmation or password reset link (ADR-0025).
/// Only a hash of the token is stored, so database access alone cannot produce a working link.
/// </summary>
public sealed class OneTimeToken : Entity
{
    /// <summary>The message for every unusable token, so callers cannot tell unknown, used and expired tokens apart.</summary>
    public const string InvalidMessage = "The link is invalid or has expired.";

    private OneTimeToken(string userId, OneTimeTokenPurpose purpose, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        UserId = userId;
        Purpose = purpose;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public string UserId { get; private set; }

    public OneTimeTokenPurpose Purpose { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When the token was redeemed or replaced by a newer one.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    public static OneTimeToken Issue(string userId, OneTimeTokenPurpose purpose, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainException("A one-time token needs a user id.");
        }

        if (userId.Length > IAuditable.UserIdMaxLength)
        {
            throw new DomainException($"User id must be at most {IAuditable.UserIdMaxLength} characters.");
        }

        return new OneTimeToken(userId, purpose, tokenHash, now, now + lifetime);
    }

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

    /// <exception cref="DomainException">The token was already used or has expired.</exception>
    public void Redeem(DateTimeOffset now)
    {
        if (!IsUsable(now))
        {
            throw new DomainException(InvalidMessage);
        }

        UsedAt = now;
    }

    /// <summary>Makes an unused token unusable, for example because a newer one was sent.</summary>
    public void Invalidate(DateTimeOffset now) => UsedAt ??= now;
}
