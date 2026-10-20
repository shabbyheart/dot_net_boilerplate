using Boilerplate.Application.Common.Auditing;
using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Roles.DeleteRole;

public sealed class DeleteRoleHandler(IAppDbContext db, RoleEscalationGuard guard, AuditLog auditLog)
{
    /// <summary>Deletes the role; its assignments are deleted with it, so the permissions stop applying at once.</summary>
    /// <exception cref="NotFoundException">No role with this id exists in the current tenant.</exception>
    /// <exception cref="ForbiddenException">The role grants a permission the caller does not hold.</exception>
    public async Task HandleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await db.Roles.GetRoleAsync(roleId, cancellationToken);
        role.EnsureDeletable();
        await guard.EnsureCanHandOutAsync(role, cancellationToken);

        var assignments = await db.RoleAssignments.CountAsync(assignment => assignment.RoleId == roleId, cancellationToken);
        db.Roles.Remove(role);
        auditLog.Record(AuditActions.RoleDeleted, role.Id, $"Deleted role '{role.Name}' and its {assignments} assignment(s).");
        await db.SaveChangesAsync(cancellationToken);
    }
}
