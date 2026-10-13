using Boilerplate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Infrastructure.Accounts;

/// <summary>
/// Identity's EF Core user store, changed to leave saving to the use case (ADR-0011, ADR-0025): account changes then
/// commit together with tokens and outbox messages, or not at all.
/// </summary>
internal sealed class UnitOfWorkUserStore : UserOnlyStore<ApplicationUser, AppDbContext, Guid>
{
    public UnitOfWorkUserStore(AppDbContext context, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
        AutoSaveChanges = false;
    }

    /// <summary>
    /// The base implementation re-attaches the user on every update. For a tracked user that resets EF Core's original
    /// concurrency stamp, so a second update before saving (such as removing and then adding a password) would make the
    /// save check a stamp the database never had. Users are always loaded through this context, so change tracking
    /// already sees every change; only the stamp needs renewing.
    /// </summary>
    public override Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        if (Context.Entry(user).State == EntityState.Detached)
        {
            throw new InvalidOperationException("Users must be loaded through the store before they are updated.");
        }

        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        return Task.FromResult(IdentityResult.Success);
    }
}
