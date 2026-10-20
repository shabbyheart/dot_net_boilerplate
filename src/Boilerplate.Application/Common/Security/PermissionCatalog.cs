using Boilerplate.Application.Features.Auditing;
using Boilerplate.Application.Features.Projects;
using Boilerplate.Application.Features.Roles;

namespace Boilerplate.Application.Common.Security;

/// <summary>
/// Every permission the application knows (ADR-0022). Roles can only contain these. A feature that adds
/// permission constants adds them here; <c>PermissionCatalogTests</c> fails otherwise.
/// </summary>
public static class PermissionCatalog
{
    public static IReadOnlyList<string> All { get; } =
    [
        ProjectPermissions.Read,
        ProjectPermissions.Manage,
        RolePermissions.Read,
        RolePermissions.Manage,
        AuditPermissions.Read,
    ];

    public static bool IsKnown(string permission) => All.Contains(permission, StringComparer.Ordinal);
}
