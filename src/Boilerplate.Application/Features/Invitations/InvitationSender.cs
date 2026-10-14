using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Messaging;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Security;
using Boilerplate.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Invitations;

/// <summary>
/// Issues an invitation in the current tenant and queues its e-mail (ADR-0027). Changes are committed by the calling
/// use case, which has already checked that the caller may hand out the role.
/// </summary>
public sealed class InvitationSender(IAppDbContext db, Outbox outbox, InvitationSettings settings, TimeProvider timeProvider)
{
    /// <summary>Creates the invitation and revokes earlier pending invitations of the same address to the same role.</summary>
    /// <exception cref="ConflictException">Invitations are not configured (no accept page).</exception>
    public async Task<Invitation> IssueAsync(Role role, string email, string tenantName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);

        var acceptUrl = settings.AcceptUrl ?? throw new ConflictException(
            $"Invitations are not configured: set {InvitationSettings.SectionName}:{nameof(InvitationSettings.AcceptUrl)}.");

        var now = timeProvider.GetUtcNow();
        var normalizedEmail = Invitation.NormalizeEmail(email);

        var earlier = await db.Invitations
            .Where(invitation => invitation.Email == normalizedEmail
                && invitation.RoleId == role.Id
                && invitation.AcceptedAt == null
                && invitation.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var invitation in earlier)
        {
            invitation.Revoke(now);
        }

        var token = SecureToken.Create();
        var issued = Invitation.Issue(normalizedEmail, role, SecureToken.Hash(token), now, settings.Lifetime);
        db.Invitations.Add(issued);
        outbox.Enqueue(InvitationEmails.Invite(normalizedEmail, tenantName, role.Name, acceptUrl, token, settings.Lifetime));

        return issued;
    }
}
