using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Invitations.GetInvitation;

public sealed class GetInvitationHandler(IAppDbContext db)
{
    /// <exception cref="NotFoundException">No invitation with this id exists in the current tenant.</exception>
    public async Task<InvitationResponse> HandleAsync(Guid invitationId, CancellationToken cancellationToken) =>
        await db.Invitations
            .AsNoTracking()
            .Where(invitation => invitation.Id == invitationId)
            .Select(InvitationResponse.FromInvitation)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Invitation", invitationId);
}
