using Boilerplate.Domain.Common;

namespace Boilerplate.Domain.Authorization;

/// <summary>
/// An e-mailed offer of a role in one tenant (ADR-0027). Whoever is signed in and presents the token first receives
/// the role. Only a hash of the token is stored; the invitation can be accepted once and is revocable until then.
/// </summary>
public sealed class Invitation : Entity, IAuditable, ITenantOwned
{
    public const int EmailMaxLength = 256;

    /// <summary>The message for every unusable token, so callers cannot tell unknown, used and expired tokens apart.</summary>
    public const string InvalidMessage = "The invitation is invalid or has expired.";

    private Invitation(string email, Guid roleId, string tokenHash, DateTimeOffset expiresAt)
    {
        Email = email;
        RoleId = roleId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    /// <summary>The invited address, trimmed and in lower case.</summary>
    public string Email { get; private set; }

    public Guid RoleId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <summary>The token subject of the user who accepted, which need not be the invited address's owner.</summary>
    public string? AcceptedBy { get; private set; }

    /// <summary>When the invitation was revoked or replaced by a newer one.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid TenantId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public static Invitation Issue(string email, Role role, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(role);
        return new Invitation(NormalizeEmail(email), role.Id, tokenHash, now + lifetime);
    }

    /// <summary>Trimmed and lower case, so one address has one spelling.</summary>
    public static string NormalizeEmail(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var trimmed = email.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException("An invitation needs an e-mail address.");
        }

        if (trimmed.Length > EmailMaxLength)
        {
            throw new DomainException($"E-mail address must be at most {EmailMaxLength} characters.");
        }

        return trimmed.ToLowerInvariant();
    }

    public bool IsPending(DateTimeOffset now) => AcceptedAt is null && RevokedAt is null && now < ExpiresAt;

    /// <exception cref="DomainException">The invitation was accepted, revoked or has expired.</exception>
    public void Accept(string userId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainException("Accepting an invitation needs a user id.");
        }

        if (!IsPending(now))
        {
            throw new DomainException(InvalidMessage);
        }

        AcceptedAt = now;
        AcceptedBy = userId;
    }

    /// <summary>Makes a pending invitation unusable. Revoking an accepted or revoked invitation changes nothing.</summary>
    public void Revoke(DateTimeOffset now)
    {
        if (AcceptedAt is null)
        {
            RevokedAt ??= now;
        }
    }
}
