namespace Boilerplate.Application.Features.Tenants.InviteTenantAdministrator;

/// <param name="Email">Who is invited to become an administrator of the tenant.</param>
public sealed record InviteTenantAdministratorRequest(string Email);
