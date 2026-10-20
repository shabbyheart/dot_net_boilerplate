using Boilerplate.Application.Common.Auditing;
using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Members;

/// <summary>
/// Takes every role of a user in the current tenant away, which ends their membership (ADR-0029). Changes are
/// committed by the calling use case, which decides whether the caller may do this.
/// </summary>
public sealed class MemberRemoval(IAppDbContext db, AuditLog auditLog)
{
    /// <summary>The user's assignments in the current tenant and their roles.</summary>
    /// <exception cref="NotFoundException">The user holds no role in the current tenant.</exception>
    public async Task<IReadOnlyList<Role>> LoadRolesAsync(string userId, CancellationToken cancellationToken)
    {
        var roleIds = await db.RoleAssignments
            .Where(assignment => assignment.UserId == userId)
            .Select(assignment => assignment.RoleId)
            .ToListAsync(cancellationToken);
        if (roleIds.Count == 0)
        {
            throw new NotFoundException("Member", userId);
        }

        return await db.Roles.Where(role => roleIds.Contains(role.Id)).ToListAsync(cancellationToken);
    }

    /// <summary>Removes the user's assignments of <paramref name="roles"/> and records each in the audit log.</summary>
    /// <exception cref="ConflictException">The user is the tenant's last administrator.</exception>
    public async Task RemoveAsync(
        string userId,
        IReadOnlyList<Role> roles,
        Func<Role, string> describe,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(describe);

        var administrators = roles.SingleOrDefault(role => role.IsAdministrator);
        if (administrators is not null
            && !await db.RoleAssignments.AnyAsync(
                assignment => assignment.RoleId == administrators.Id && assignment.UserId != userId,
                cancellationToken))
        {
            throw new ConflictException(
                $"The last {Role.AdministratorsName} member cannot be removed; the tenant would have no administrator.");
        }

        var assignments = await db.RoleAssignments
            .Where(assignment => assignment.UserId == userId)
            .ToListAsync(cancellationToken);
        db.RoleAssignments.RemoveRange(assignments);

        foreach (var role in roles)
        {
            auditLog.Record(AuditActions.RoleUnassigned, role.Id, describe(role));
        }
    }
}
