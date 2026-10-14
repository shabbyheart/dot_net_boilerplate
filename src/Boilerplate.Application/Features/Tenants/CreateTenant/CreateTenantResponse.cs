using Boilerplate.Application.Features.Invitations;

namespace Boilerplate.Application.Features.Tenants.CreateTenant;

/// <summary>The new tenant and the invitation sent to its first administrator.</summary>
public sealed record CreateTenantResponse(TenantResponse Tenant, InvitationResponse AdministratorInvitation);
