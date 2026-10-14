using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Invitations.ListInvitations;

public sealed class ListInvitationsHandler(IAppDbContext db)
{
    public Task<PagedResponse<InvitationResponse>> HandleAsync(ListInvitationsRequest request, CancellationToken cancellationToken) =>
        db.Invitations
            .AsNoTracking()
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ThenByDescending(invitation => invitation.Id)
            .Select(InvitationResponse.FromInvitation)
            .ToPagedResponseAsync(request, cancellationToken);
}
