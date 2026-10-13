namespace Boilerplate.Application.Features.Tenants.BootstrapTenant;

/// <summary>What the bootstrap changed. All flags are false when it had already been done.</summary>
public sealed record BootstrapTenantResult(Guid TenantId, bool TenantCreated, bool AdministratorRoleCreated, bool AdministratorAssigned);
