namespace Boilerplate.Application.Features.Roles;

/// <summary>Permissions of role administration (ADR-0022).</summary>
public static class RolePermissions
{
    /// <summary>List and view roles, their assignments and the permission catalogue.</summary>
    public const string Read = "roles.read";

    /// <summary>Create, change and delete roles, and assign them to users.</summary>
    public const string Manage = "roles.manage";
}
