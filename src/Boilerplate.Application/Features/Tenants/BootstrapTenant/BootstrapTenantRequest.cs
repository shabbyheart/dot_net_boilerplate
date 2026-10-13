namespace Boilerplate.Application.Features.Tenants.BootstrapTenant;

/// <param name="TenantName">The tenant to create or reuse, identified by its exact name.</param>
/// <param name="AdminUserId">The token subject of the user who becomes an administrator of the tenant.</param>
public sealed record BootstrapTenantRequest(string TenantName, string AdminUserId);
