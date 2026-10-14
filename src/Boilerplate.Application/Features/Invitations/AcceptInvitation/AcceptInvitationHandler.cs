using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Security;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Application.Features.Roles;
using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Invitations.AcceptInvitation;

public sealed class AcceptInvitationHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    TenantOverride tenantOverride,
    TimeProvider timeProvider)
{
    /// <summary>Gives the signed-in caller the invitation's role in the invitation's tenant (ADR-0027).</summary>
    /// <exception cref="DomainException">The token is unknown, accepted, revoked or expired.</exception>
    public async Task<AcceptInvitationResponse> HandleAsync(AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Accepting an invitation requires an authenticated user.");

        // The caller is not a member of the tenant yet, so this reviewed query bypasses the tenant filter; the token
        // hash identifies exactly one invitation (ADR-0010, ADR-0027).
        var hash = SecureToken.Hash(request.Token);
        var invitation = await db.Invitations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(i => i.TokenHash == hash, cancellationToken)
            ?? throw new DomainException(Invitation.InvalidMessage);

        invitation.Accept(userId, timeProvider.GetUtcNow());

        // From here on, reads and writes act for the invitation's tenant.
        tenantOverride.Set(invitation.TenantId);

        var role = await db.Roles.GetRoleAsync(invitation.RoleId, cancellationToken);
        var alreadyAssigned = await db.RoleAssignments
            .AnyAsync(assignment => assignment.RoleId == role.Id && assignment.UserId == userId, cancellationToken);
        if (!alreadyAssigned)
        {
            db.RoleAssignments.Add(RoleAssignment.Create(userId, role));
        }

        // The invitation's row version stops two concurrent acceptances from both succeeding (409).
        await db.SaveChangesAsync(cancellationToken);

        return new AcceptInvitationResponse(invitation.TenantId, role.Id);
    }
}
