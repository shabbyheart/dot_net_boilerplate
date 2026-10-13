using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Common.Security;

/// <summary>
/// Decides whether the current user holds a permission in the current tenant (ADR-0017).
/// One indexed query per check and no cache, so a revocation is effective on the next request on every instance.
/// </summary>
public sealed class PermissionChecker(IAppDbContext db, ICurrentUser currentUser, ITenantContext tenantContext)
{
    public async Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        var userId = currentUser.UserId;
        if (userId is null || tenantContext.TenantId is null)
        {
            return false;
        }

        // The tenant query filter applies to both roles and assignments, so only the current tenant's data counts.
        return await db.RoleAssignments
            .Where(assignment => assignment.UserId == userId)
            .Join(db.Roles, assignment => assignment.RoleId, role => role.Id, (_, role) => role)
            .SelectMany(role => role.Permissions)
            .AnyAsync(granted => granted.Name == permission, cancellationToken);
    }
}
