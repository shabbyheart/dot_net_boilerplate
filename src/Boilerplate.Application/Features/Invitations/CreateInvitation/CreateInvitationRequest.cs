namespace Boilerplate.Application.Features.Invitations.CreateInvitation;

/// <param name="Email">Where the invitation is sent.</param>
/// <param name="RoleId">The role of the current tenant that the invitation offers.</param>
public sealed record CreateInvitationRequest(string Email, Guid RoleId);
