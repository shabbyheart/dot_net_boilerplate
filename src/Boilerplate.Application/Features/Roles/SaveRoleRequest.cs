namespace Boilerplate.Application.Features.Roles;

/// <summary>The editable state of a role, used to create one and to replace an existing one.</summary>
/// <param name="Name">Required; unique within the tenant; at most 100 characters.</param>
/// <param name="Permissions">The complete set of granted permissions; each must be in the permission catalogue.</param>
public sealed record SaveRoleRequest(string Name, IReadOnlyList<string>? Permissions)
{
    public IReadOnlyList<string> PermissionsOrEmpty => Permissions ?? [];
}
