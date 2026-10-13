using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Security;
using Boilerplate.Domain.Authorization;

namespace Boilerplate.Application.Features.Roles;

/// <summary>
/// Prevents privilege escalation in role administration (ADR-0022): callers can grant, remove or hand out only
/// permissions they hold themselves, and only administrators can manage the administrator role.
/// </summary>
public sealed class RoleEscalationGuard(PermissionChecker permissionChecker)
{
    /// <summary>Every permission added to or removed from a role must be held by the caller.</summary>
    public async Task EnsureCanChangeAsync(IEnumerable<string> changedPermissions, CancellationToken cancellationToken)
    {
        var caller = await permissionChecker.GetCallerPermissionsAsync(cancellationToken);
        var notHeld = changedPermissions.Where(permission => !caller.Holds(permission)).Order(StringComparer.Ordinal).ToList();

        if (notHeld.Count > 0)
        {
            throw new ForbiddenException(
                $"You can only grant or remove permissions you hold yourself. Not held: {string.Join(", ", notHeld)}.");
        }
    }

    /// <summary>The caller must hold everything the role grants to assign, unassign or delete it.</summary>
    public async Task EnsureCanHandOutAsync(Role role, CancellationToken cancellationToken)
    {
        if (role.IsAdministrator)
        {
            var caller = await permissionChecker.GetCallerPermissionsAsync(cancellationToken);
            if (!caller.IsAdministrator)
            {
                throw new ForbiddenException($"Only administrators can manage the {Role.AdministratorsName} role.");
            }

            return;
        }

        await EnsureCanChangeAsync(role.Permissions.Select(granted => granted.Name), cancellationToken);
    }
}
