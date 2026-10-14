using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Tenants;

/// <summary>Query logic shared by several tenant handlers.</summary>
internal static class TenantQueries
{
    /// <exception cref="NotFoundException">No tenant with this id exists.</exception>
    public static async Task<Tenant> GetTenantAsync(this IQueryable<Tenant> tenants, Guid tenantId, CancellationToken cancellationToken) =>
        await tenants.SingleOrDefaultAsync(tenant => tenant.Id == tenantId, cancellationToken)
        ?? throw new NotFoundException("Tenant", tenantId);

    /// <summary>Whether another tenant already uses <paramref name="name"/> (exact match; names are unique, ADR-0022).</summary>
    public static Task<bool> NameIsTakenAsync(
        this IQueryable<Tenant> tenants,
        string name,
        Guid excludingTenantId,
        CancellationToken cancellationToken) =>
        tenants.AnyAsync(tenant => tenant.Name == name && tenant.Id != excludingTenantId, cancellationToken);

    /// <summary>The current tenant's administrator role, or <see langword="null"/> if it has none yet.</summary>
    public static Task<Role?> FindAdministratorsAsync(this IQueryable<Role> roles, CancellationToken cancellationToken) =>
        roles.SingleOrDefaultAsync(role => role.IsAdministrator, cancellationToken);
}
