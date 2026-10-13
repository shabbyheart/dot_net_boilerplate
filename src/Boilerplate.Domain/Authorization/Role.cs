using Boilerplate.Domain.Common;

namespace Boilerplate.Domain.Authorization;

/// <summary>
/// A named set of permissions within one tenant (ADR-0017). The built-in administrator role grants every
/// permission implicitly and cannot be changed or deleted (ADR-0022).
/// </summary>
public sealed class Role : Entity, IAuditable, ITenantOwned
{
    public const int NameMaxLength = 100;
    public const string AdministratorsName = "Administrators";

    private readonly List<PermissionGrant> _permissions = [];

    private Role(string name, bool isAdministrator)
    {
        Name = name;
        IsAdministrator = isAdministrator;
    }

    public string Name { get; private set; }

    /// <summary>The built-in role that grants every permission, including ones added later.</summary>
    public bool IsAdministrator { get; private set; }

    /// <summary>The permissions granted explicitly. Always empty for the administrator role.</summary>
    public IReadOnlyCollection<PermissionGrant> Permissions => _permissions.AsReadOnly();

    public Guid TenantId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public static Role Create(string name) => new(NormalizeName(name), isAdministrator: false);

    public static Role CreateAdministrators() => new(AdministratorsName, isAdministrator: true);

    public void Rename(string name)
    {
        EnsureEditable();
        Name = NormalizeName(name);
    }

    /// <summary>Grants <paramref name="permission"/>; granting a permission the role already has changes nothing.</summary>
    public void Grant(string permission)
    {
        EnsureEditable();
        var canonical = PermissionName.Validate(permission);
        if (!HasPermission(canonical))
        {
            _permissions.Add(new PermissionGrant(canonical));
        }
    }

    /// <summary>Revokes <paramref name="permission"/>; the change is effective on the next request (ADR-0017).</summary>
    public void Revoke(string permission)
    {
        EnsureEditable();
        _permissions.RemoveAll(granted => string.Equals(granted.Name, permission, StringComparison.Ordinal));
    }

    /// <summary>Makes <paramref name="permissions"/> the role's exact set of permissions.</summary>
    public void ReplacePermissions(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        EnsureEditable();

        var target = permissions.Select(PermissionName.Validate).ToHashSet(StringComparer.Ordinal);
        _permissions.RemoveAll(granted => !target.Contains(granted.Name));
        foreach (var permission in target.Where(permission => !HasPermission(permission)))
        {
            _permissions.Add(new PermissionGrant(permission));
        }
    }

    /// <summary>Whether the role grants <paramref name="permission"/>, implicitly for the administrator role.</summary>
    public bool HasPermission(string permission) =>
        IsAdministrator
        || _permissions.Exists(granted => string.Equals(granted.Name, permission, StringComparison.Ordinal));

    /// <summary>Throws when the role must not be deleted (the administrator role).</summary>
    public void EnsureDeletable()
    {
        if (IsAdministrator)
        {
            throw new DomainException($"The {AdministratorsName} role cannot be deleted.");
        }
    }

    private void EnsureEditable()
    {
        if (IsAdministrator)
        {
            throw new DomainException($"The {AdministratorsName} role cannot be changed.");
        }
    }

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
