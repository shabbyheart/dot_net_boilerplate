using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Members.ListMembers;

public sealed class ListMembersHandler(IAppDbContext db)
{
    public async Task<PagedResponse<MemberResponse>> HandleAsync(ListMembersRequest request, CancellationToken cancellationToken)
    {
        var page = await db.RoleAssignments
            .AsNoTracking()
            .Summaries()
            .ToPagedResponseAsync(request, cancellationToken);

        var members = await db.WithRolesAsync(page.Items, cancellationToken);
        return new PagedResponse<MemberResponse>(members, page.Page, page.PageSize, page.TotalCount);
    }
}
