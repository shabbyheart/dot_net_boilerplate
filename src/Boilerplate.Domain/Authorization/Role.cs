using Boilerplate.Domain.Common;

namespace Boilerplate.Domain.Authorization;

/// <summary>A named set of permissions within one tenant (ADR-0017).</summary>
public sealed class Role : Entity, IAuditable, ITenantOwned
{
    public const int NameMaxLength = 100;

    private readonly List<PermissionGrant> _permissions = [];

    private Role(string name)
    {
        Name = name;
    }

    public string Name { get; private set; }

    public IReadOnlyCollection<PermissionGrant> Permissions => _permissions.AsReadOnly();

    public Guid TenantId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public static Role Create(string name) => new(NormalizeName(name));

    public void Rename(string name) => Name = NormalizeName(name);

    /// <summary>Grants <paramref name="permission"/>; granting a permission the role already has changes nothing.</summary>
    public void Grant(string permission)
    {
        var canonical = PermissionName.Validate(permission);
        if (!HasPermission(canonical))
        {
            _permissions.Add(new PermissionGrant(canonical));
        }
    }

    /// <summary>Revokes <paramref name="permission"/>; the change is effective on the next request (ADR-0017).</summary>
    public void Revoke(string permission) =>
        _permissions.RemoveAll(granted => string.Equals(granted.Name, permission, StringComparison.Ordinal));

    public bool HasPermission(string permission) =>
        _permissions.Exists(granted => string.Equals(granted.Name, permission, StringComparison.Ordinal));

    private static string NormalizeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException("Role name must not be empty.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException($"Role name must be at most {NameMaxLength} characters.");
        }

        return trimmed;
    }
}
