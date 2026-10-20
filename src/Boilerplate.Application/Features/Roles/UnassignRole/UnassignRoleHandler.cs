using Boilerplate.Application.Common.Auditing;
using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Roles.UnassignRole;

public sealed class UnassignRoleHandler(IAppDbContext db, RoleEscalationGuard guard, AuditLog auditLog)
{
    /// <summary>Takes the role away from the user; effective on their next request (ADR-0017).</summary>
    /// <exception cref="NotFoundException">The role does not exist, or the user does not hold it.</exception>
    /// <exception cref="ForbiddenException">The role grants a permission the caller does not hold.</exception>
    /// <exception cref="ConflictException">It is the last assignment of the administrator role.</exception>
    public async Task HandleAsync(Guid roleId, string userId, CancellationToken cancellationToken)
    {
        var role = await db.Roles.GetRoleAsync(roleId, cancellationToken);
        await guard.EnsureCanHandOutAsync(role, cancellationToken);

        var assignment = await db.RoleAssignments
            .SingleOrDefaultAsync(a => a.RoleId == roleId && a.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Role assignment", userId);

        if (role.IsAdministrator && await db.RoleAssignments.CountAsync(a => a.RoleId == roleId, cancellationToken) == 1)
        {
            throw new ConflictException(
                $"The last {Role.AdministratorsName} assignment cannot be removed; the tenant would have no administrator.");
        }

        db.RoleAssignments.Remove(assignment);
        auditLog.Record(AuditActions.RoleUnassigned, role.Id, $"Removed role '{role.Name}' from user {userId}.");
        await db.SaveChangesAsync(cancellationToken);
    }
}
