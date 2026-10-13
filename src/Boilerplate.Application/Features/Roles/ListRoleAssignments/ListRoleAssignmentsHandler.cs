using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Roles.ListRoleAssignments;

public sealed class ListRoleAssignmentsHandler(IAppDbContext db)
{
    /// <exception cref="NotFoundException">No role with this id exists in the current tenant.</exception>
    public async Task<PagedResponse<RoleAssignmentResponse>> HandleAsync(
        Guid roleId,
        ListRoleAssignmentsRequest request,
        CancellationToken cancellationToken)
    {
        if (!await db.Roles.AnyAsync(role => role.Id == roleId, cancellationToken))
        {
            throw new NotFoundException("Role", roleId);
        }

        return await db.RoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.RoleId == roleId)
            .OrderBy(assignment => assignment.UserId)
            .ThenBy(assignment => assignment.Id)
            .Select(RoleAssignmentResponse.FromAssignment)
            .ToPagedResponseAsync(request, cancellationToken);
    }
}
