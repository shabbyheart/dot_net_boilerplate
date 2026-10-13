using Boilerplate.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Accounts;

internal static class AccountQueries
{
    /// <summary>
    /// Whether the user holds any role in the tenant. Sign-in has no tenant yet, so this reviewed query bypasses the
    /// tenant filter and names the tenant explicitly (ADR-0010, ADR-0025).
    /// </summary>
    public static Task<bool> IsTenantMemberAsync(
        this IQueryable<RoleAssignment> assignments,
        string userId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        assignments
            .IgnoreQueryFilters()
            .AnyAsync(assignment => assignment.TenantId == tenantId && assignment.UserId == userId, cancellationToken);
}
