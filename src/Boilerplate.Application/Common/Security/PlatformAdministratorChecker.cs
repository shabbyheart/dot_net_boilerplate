using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Common.Security;

/// <summary>
/// Decides whether the current user is a platform administrator (ADR-0027). Independent of the current tenant.
/// One indexed query per call and no cache, so revoking the grant is effective on the next request (ADR-0017).
/// </summary>
public sealed class PlatformAdministratorChecker(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<bool> IsPlatformAdministratorAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        return userId is not null
            && await db.PlatformAdministrators.AnyAsync(administrator => administrator.UserId == userId, cancellationToken);
    }
}
