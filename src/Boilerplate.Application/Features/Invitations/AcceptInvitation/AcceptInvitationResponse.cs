namespace Boilerplate.Application.Features.Invitations.AcceptInvitation;

/// <summary>The tenant and role the caller now holds. Sign in for this tenant to act in it.</summary>
public sealed record AcceptInvitationResponse(Guid TenantId, Guid RoleId);
