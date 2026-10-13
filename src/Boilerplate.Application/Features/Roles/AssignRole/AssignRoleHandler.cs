using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Roles.AssignRole;

public sealed class AssignRoleHandler(IAppDbContext db, RoleEscalationGuard guard)
{
    /// <summary>Gives the user the role. Assigning a role the user already holds changes nothing.</summary>
    /// <exception cref="NotFoundException">No role with this id exists in the current tenant.</exception>
    /// <exception cref="ForbiddenException">The role grants a permission the caller does not hold.</exception>
    public async Task HandleAsync(Guid roleId, string userId, CancellationToken cancellationToken)
    {
        var role = await db.Roles.GetRoleAsync(roleId, cancellationToken);
        await guard.EnsureCanHandOutAsync(role, cancellationToken);

        var alreadyAssigned = await db.RoleAssignments
            .AnyAsync(assignment => assignment.RoleId == roleId && assignment.UserId == userId, cancellationToken);
        if (alreadyAssigned)
        {
            return;
        }

        db.RoleAssignments.Add(RoleAssignment.Create(userId, role));
        await db.SaveChangesAsync(cancellationToken);
    }
}
