using System.Linq.Expressions;
using Boilerplate.Domain.Tenants;

namespace Boilerplate.Application.Features.Tenants;

/// <summary>A tenant as returned to platform administrators (ADR-0027).</summary>
public sealed record TenantResponse(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    /// <summary>Hand-written mapping (ADR-0015); EF Core translates it to a column projection in queries.</summary>
    public static readonly Expression<Func<Tenant, TenantResponse>> FromTenant =
        tenant => new TenantResponse(tenant.Id, tenant.Name, tenant.CreatedAt, tenant.UpdatedAt);

    private static readonly Func<Tenant, TenantResponse> MapTenant = FromTenant.Compile();

    /// <summary>Maps an entity already in memory (after a save), with the same mapping as queries.</summary>
    public static TenantResponse From(Tenant tenant) => MapTenant(tenant);
}
