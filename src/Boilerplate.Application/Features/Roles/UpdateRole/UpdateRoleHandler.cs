using Boilerplate.Application.Common.Auditing;
using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;

namespace Boilerplate.Application.Features.Roles.UpdateRole;

public sealed class UpdateRoleHandler(IAppDbContext db, RoleEscalationGuard guard, AuditLog auditLog)
{
    /// <exception cref="NotFoundException">No role with this id exists in the current tenant.</exception>
    /// <exception cref="ForbiddenException">A permission being added or removed is not held by the caller.</exception>
    /// <exception cref="ConflictException">Another role already uses the new name.</exception>
    public async Task<RoleResponse> HandleAsync(Guid roleId, SaveRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await db.Roles.GetRoleAsync(roleId, cancellationToken);

        // Only the difference matters: unchanged permissions need not be held by the caller.
        var current = role.Permissions.Select(granted => granted.Name).ToHashSet(StringComparer.Ordinal);
        var requested = request.PermissionsOrEmpty.ToHashSet(StringComparer.Ordinal);
        var changed = new HashSet<string>(current, StringComparer.Ordinal);
        changed.SymmetricExceptWith(requested);
        await guard.EnsureCanChangeAsync(changed, cancellationToken);

        var previousName = role.Name;
        role.Rename(request.Name);
        role.ReplacePermissions(requested);

        if (await db.Roles.NameIsTakenAsync(role.Name, role.Id, cancellationToken))
        {
            throw new ConflictException($"A role named '{role.Name}' already exists.");
        }

        // Saving the same name and permissions again is not a change worth recording.
        if (changed.Count > 0 || !string.Equals(role.Name, previousName, StringComparison.Ordinal))
        {
            auditLog.Record(AuditActions.RoleUpdated, role.Id, RoleAuditing.Updated(previousName, role.Name, current, requested));
        }

        await db.SaveChangesAsync(cancellationToken);

        return RoleResponse.From(role);
    }
}
