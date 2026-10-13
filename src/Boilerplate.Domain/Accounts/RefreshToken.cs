using Boilerplate.Domain.Common;

namespace Boilerplate.Domain.Accounts;

/// <summary>
/// A refresh token issued by the local account module (ADR-0025). Only a hash of the token is stored.
/// Every use replaces it with a new token in the same family; presenting a replaced token again means it was
/// stolen or replayed, and the whole family is revoked.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken(string userId, Guid? tenantId, Guid familyId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        UserId = userId;
        TenantId = tenantId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public string UserId { get; private set; }

    /// <summary>The tenant the session acts for, or <see langword="null"/> for a session without a tenant.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Shared by every token issued from one sign-in.</summary>
    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When the token was used (and replaced), revoked, or its family was revoked.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedById { get; private set; }

    /// <summary>Starts a new family for a sign-in.</summary>
    public static RefreshToken Issue(string userId, Guid? tenantId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainException("A refresh token needs a user id.");
        }

        if (userId.Length > IAuditable.UserIdMaxLength)
        {
            throw new DomainException($"User id must be at most {IAuditable.UserIdMaxLength} characters.");
        }

        return new RefreshToken(userId, tenantId, Guid.CreateVersion7(), tokenHash, now, now + lifetime);
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    /// <summary>Uses this token: it is revoked and replaced by a new token in the same family.</summary>
    public RefreshToken Rotate(string newTokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (!IsActive(now))
        {
            throw new DomainException("The refresh token is no longer valid.");
        }

        var next = new RefreshToken(UserId, TenantId, FamilyId, newTokenHash, now, now + lifetime);
        RevokedAt = now;
        ReplacedById = next.Id;
        return next;
    }

    /// <summary>Revokes the token. Revoking a revoked token keeps the original time.</summary>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
