using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Features.Roles;

namespace Boilerplate.Application.Features.Members.RemoveMember;

public sealed class RemoveMemberHandler(IAppDbContext db, RoleEscalationGuard guard, MemberRemoval removal)
{
    /// <summary>Takes every role of the user in the current tenant away; effective on their next request (ADR-0029).</summary>
    /// <exception cref="NotFoundException">The user holds no role in the current tenant.</exception>
    /// <exception cref="ForbiddenException">The user holds a role the caller could not hand out (ADR-0022).</exception>
    /// <exception cref="ConflictException">The user is the tenant's last administrator.</exception>
    public async Task HandleAsync(string userId, CancellationToken cancellationToken)
    {
        var roles = await removal.LoadRolesAsync(userId, cancellationToken);
        foreach (var role in roles)
        {
            await guard.EnsureCanHandOutAsync(role, cancellationToken);
        }

        await removal.RemoveAsync(
            userId,
            roles,
            role => $"Removed user {userId} from the tenant, taking away role '{role.Name}'.",
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
