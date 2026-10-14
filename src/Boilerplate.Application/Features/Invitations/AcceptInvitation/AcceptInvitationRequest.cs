namespace Boilerplate.Application.Features.Invitations.AcceptInvitation;

/// <param name="Token">The token from the invitation link.</param>
public sealed record AcceptInvitationRequest(string Token);
