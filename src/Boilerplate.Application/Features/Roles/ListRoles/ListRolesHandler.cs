using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Roles.ListRoles;

public sealed class ListRolesHandler(IAppDbContext db)
{
    public Task<PagedResponse<RoleResponse>> HandleAsync(ListRolesRequest request, CancellationToken cancellationToken) =>
        db.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .ThenBy(role => role.Id)
            .Select(RoleResponse.FromRole)
            .ToPagedResponseAsync(request, cancellationToken);
}
