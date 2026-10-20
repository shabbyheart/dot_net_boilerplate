using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Security;

namespace Boilerplate.Application.Features.Members.LeaveTenant;

public sealed class LeaveTenantHandler(IAppDbContext db, ICurrentUser currentUser, MemberRemoval removal)
{
    /// <summary>
    /// The caller gives up every role they hold in the current tenant (ADR-0029). No permission is needed: giving up
    /// one's own roles cannot escalate.
    /// </summary>
    /// <exception cref="NotFoundException">The caller holds no role in the current tenant (or the token names none).</exception>
    /// <exception cref="ConflictException">The caller is the tenant's last administrator.</exception>
    public async Task HandleAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Leaving a tenant requires an authenticated user.");

        var roles = await removal.LoadRolesAsync(userId, cancellationToken);
        await removal.RemoveAsync(
            userId,
            roles,
            role => $"User {userId} left the tenant, giving up role '{role.Name}'.",
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
