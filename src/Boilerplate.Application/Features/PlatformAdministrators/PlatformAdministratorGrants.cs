using Boilerplate.Application.Common.Persistence;
using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.PlatformAdministrators;

/// <summary>
/// Grants and revokes platform administration (ADR-0027). Used only by the command line: no HTTP endpoint can make
/// anyone a platform administrator, so a stolen token cannot create more of them.
/// </summary>
public sealed class PlatformAdministratorGrants(IAppDbContext db)
{
    /// <returns><see langword="false"/> if the user already was a platform administrator.</returns>
    /// <exception cref="DomainException">The user id is empty or too long.</exception>
    public async Task<bool> GrantAsync(string userId, CancellationToken cancellationToken)
    {
        var administrator = PlatformAdministrator.Create(userId);
        if (await db.PlatformAdministrators.AnyAsync(a => a.UserId == administrator.UserId, cancellationToken))
        {
            return false;
        }

        db.PlatformAdministrators.Add(administrator);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <returns><see langword="false"/> if the user was not a platform administrator.</returns>
    public async Task<bool> RevokeAsync(string userId, CancellationToken cancellationToken) =>
        await db.PlatformAdministrators.Where(a => a.UserId == userId).ExecuteDeleteAsync(cancellationToken) > 0;
}
