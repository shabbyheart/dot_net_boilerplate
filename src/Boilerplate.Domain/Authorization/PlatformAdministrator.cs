using Boilerplate.Domain.Common;

namespace Boilerplate.Domain.Authorization;

/// <summary>
/// A user who administers tenants across the platform (ADR-0027), identified by the token subject. Not tenant-owned
/// and unrelated to tenant roles: it grants no permission inside any tenant. Granted only by the command line.
/// </summary>
public sealed class PlatformAdministrator : Entity, IAuditable
{
    private PlatformAdministrator(string userId)
    {
        UserId = userId;
    }

    public string UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public static PlatformAdministrator Create(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainException("A platform administrator needs a user id.");
        }

        if (userId.Length > IAuditable.UserIdMaxLength)
        {
            throw new DomainException($"User id must be at most {IAuditable.UserIdMaxLength} characters.");
        }

        return new PlatformAdministrator(userId);
    }
}
