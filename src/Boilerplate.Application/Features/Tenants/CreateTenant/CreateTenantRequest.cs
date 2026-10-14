namespace Boilerplate.Application.Features.Tenants.CreateTenant;

/// <param name="Name">The new tenant's name; unique across the platform.</param>
/// <param name="AdministratorEmail">Who is invited to become the tenant's first administrator.</param>
public sealed record CreateTenantRequest(string Name, string AdministratorEmail);
