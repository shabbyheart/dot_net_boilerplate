namespace Boilerplate.Domain.Authorization;

/// <summary>A permission granted by a <see cref="Role"/>. Part of the role; it has no identity of its own.</summary>
public sealed class PermissionGrant
{
    internal PermissionGrant(string name)
    {
        Name = name;
    }

    public string Name { get; private set; }
}
