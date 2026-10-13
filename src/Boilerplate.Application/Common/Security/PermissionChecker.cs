using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Common.Security;

/// <summary>
/// Decides what the current user may do in the current tenant (ADR-0017, ADR-0022).
/// One indexed query per call and no cache, so a revocation is effective on the next request on every instance.
/// The tenant query filter applies to roles and assignments, so only the current tenant's data counts.
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

        return await RolesOf(userId)
            .AnyAsync(
                role => role.IsAdministrator || role.Permissions.Any(granted => granted.Name == permission),
                cancellationToken);
    }

    public async Task<CallerPermissions> GetCallerPermissionsAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null || tenantContext.TenantId is null)
        {
            return CallerPermissions.None;
        }

        var roles = await RolesOf(userId)
            .Select(role => new { role.IsAdministrator, Permissions = role.Permissions.Select(granted => granted.Name).ToList() })
            .ToListAsync(cancellationToken);

        return new CallerPermissions(
            roles.Exists(role => role.IsAdministrator),
            roles.SelectMany(role => role.Permissions).ToHashSet(StringComparer.Ordinal));
    }

    private IQueryable<Domain.Authorization.Role> RolesOf(string userId) =>
        db.RoleAssignments
            .Where(assignment => assignment.UserId == userId)
            .Join(db.Roles, assignment => assignment.RoleId, role => role.Id, (_, role) => role);
}
