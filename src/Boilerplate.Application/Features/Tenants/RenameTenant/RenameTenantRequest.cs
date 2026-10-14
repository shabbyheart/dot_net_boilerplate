namespace Boilerplate.Application.Features.Tenants.RenameTenant;

/// <param name="Name">The tenant's new name; unique across the platform.</param>
public sealed record RenameTenantRequest(string Name);
