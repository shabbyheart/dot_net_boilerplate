using System.Linq.Expressions;
using Boilerplate.Domain.Authorization;

namespace Boilerplate.Application.Features.Invitations;

/// <summary>An invitation as returned by the API. The token is never returned; it exists only in the e-mail.</summary>
public sealed record InvitationResponse(
    Guid Id,
    string Email,
    Guid RoleId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AcceptedAt,
    string? AcceptedBy,
    DateTimeOffset? RevokedAt,
    DateTimeOffset CreatedAt)
{
    /// <summary>Hand-written mapping (ADR-0015); EF Core translates it to a column projection in queries.</summary>
    public static readonly Expression<Func<Invitation, InvitationResponse>> FromInvitation = invitation => new InvitationResponse(
        invitation.Id,
        invitation.Email,
        invitation.RoleId,
        invitation.ExpiresAt,
        invitation.AcceptedAt,
        invitation.AcceptedBy,
        invitation.RevokedAt,
        invitation.CreatedAt);

    private static readonly Func<Invitation, InvitationResponse> MapInvitation = FromInvitation.Compile();

    /// <summary>Maps an entity already in memory (after a save), with the same mapping as queries.</summary>
    public static InvitationResponse From(Invitation invitation) => MapInvitation(invitation);
}
