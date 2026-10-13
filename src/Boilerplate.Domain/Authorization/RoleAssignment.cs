using Boilerplate.Domain.Common;

namespace Boilerplate.Domain.Authorization;

/// <summary>
/// Gives a user a role within one tenant (ADR-0017). The user is identified by the token's subject (ADR-0016),
/// because users live in the identity provider: an external one, or the optional local accounts (ADR-0025).
/// </summary>
public sealed class RoleAssignment : Entity, IAuditable, ITenantOwned
{
    private RoleAssignment(string userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }

    public string UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public Guid TenantId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public static RoleAssignment Create(string userId, Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainException("A role assignment needs a user id.");
        }

        if (userId.Length > IAuditable.UserIdMaxLength)
        {
            throw new DomainException($"User id must be at most {IAuditable.UserIdMaxLength} characters.");
        }

        return new RoleAssignment(userId, role.Id);
    }
}
