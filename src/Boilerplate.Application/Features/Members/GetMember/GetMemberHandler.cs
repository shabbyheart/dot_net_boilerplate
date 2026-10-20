using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Members.GetMember;

public sealed class GetMemberHandler(IAppDbContext db)
{
    /// <exception cref="NotFoundException">The user holds no role in the current tenant.</exception>
    public async Task<MemberResponse> HandleAsync(string userId, CancellationToken cancellationToken)
    {
        var member = await db.RoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.UserId == userId)
            .Summaries()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Member", userId);

        return (await db.WithRolesAsync([member], cancellationToken))[0];
    }
}
