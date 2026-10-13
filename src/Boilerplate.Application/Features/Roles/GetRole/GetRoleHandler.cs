using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Roles.GetRole;

public sealed class GetRoleHandler(IAppDbContext db)
{
    /// <exception cref="NotFoundException">No role with this id exists in the current tenant.</exception>
    public async Task<RoleResponse> HandleAsync(Guid roleId, CancellationToken cancellationToken) =>
        await db.Roles
            .AsNoTracking()
            .Where(role => role.Id == roleId)
            .Select(RoleResponse.FromRole)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Role", roleId);
}
