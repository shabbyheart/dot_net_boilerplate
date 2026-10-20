using Boilerplate.Application.Common.Auditing;
using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Features.Roles;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Invitations.RevokeInvitation;

public sealed class RevokeInvitationHandler(
    IAppDbContext db,
    RoleEscalationGuard guard,
    AuditLog auditLog,
    TimeProvider timeProvider)
{
    /// <summary>Makes a pending invitation unusable. Revoking an accepted or revoked invitation changes nothing.</summary>
    /// <exception cref="NotFoundException">No invitation with this id exists in the current tenant.</exception>
    /// <exception cref="ForbiddenException">The caller could not assign the invitation's role themselves.</exception>
    public async Task HandleAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await db.Invitations.SingleOrDefaultAsync(i => i.Id == invitationId, cancellationToken)
            ?? throw new NotFoundException("Invitation", invitationId);
        var role = await db.Roles.GetRoleAsync(invitation.RoleId, cancellationToken);
        await guard.EnsureCanHandOutAsync(role, cancellationToken);

        var wasRevoked = invitation.RevokedAt is not null;
        invitation.Revoke(timeProvider.GetUtcNow());
        if (!wasRevoked && invitation.RevokedAt is not null)
        {
            auditLog.Record(AuditActions.InvitationRevoked, invitation.Id, $"Revoked an invitation to role '{role.Name}'.");
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
