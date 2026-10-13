using Boilerplate.Application.Features.Accounts;
using Boilerplate.Domain.Common;
using Boilerplate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace Boilerplate.Infrastructure.Accounts;

/// <summary>
/// <see cref="IUserAccounts"/> with ASP.NET Core Identity (ADR-0025). The user store does not save on its own
/// (<c>AutoSaveChanges = false</c>), so account changes commit together with the use case's other changes.
/// </summary>
internal sealed class IdentityUserAccounts(
    UserManager<ApplicationUser> users,
    IPasswordHasher<ApplicationUser> passwordHasher,
    AppDbContext db) : IUserAccounts
{
    // Hashed once per process: verifying against it costs as much as a real check, so unknown addresses take as long.
    private static readonly Lazy<string> UnknownUserHash =
        new(() => new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString()));

    public async Task<Registration> RegisterAsync(string email, string password, CancellationToken cancellationToken)
    {
        var existing = await users.FindByEmailAsync(email);
        if (existing is null)
        {
            var user = new ApplicationUser { UserName = email, Email = email };
            ThrowIfFailed(await users.CreateAsync(user, password));
            return new Registration(user.Id.ToString(), AlreadyConfirmed: false);
        }

        if (existing.EmailConfirmed)
        {
            return new Registration(existing.Id.ToString(), AlreadyConfirmed: true);
        }

        // Whoever registered last (and can read the mailbox) owns the unconfirmed account.
        ThrowIfFailed(await users.RemovePasswordAsync(existing));
        ThrowIfFailed(await users.AddPasswordAsync(existing, password));
        return new Registration(existing.Id.ToString(), AlreadyConfirmed: false);
    }

    public async Task<PasswordCheck> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(new ApplicationUser(), UnknownUserHash.Value, password);
            return new PasswordCheck(PasswordCheckStatus.Failed);
        }

        if (await users.IsLockedOutAsync(user))
        {
            return new PasswordCheck(PasswordCheckStatus.LockedOut);
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            // Counts towards lockout; locks the account when the limit is reached.
            await users.AccessFailedAsync(user);
            await db.SaveChangesAsync(cancellationToken);

            return new PasswordCheck(await users.IsLockedOutAsync(user) ? PasswordCheckStatus.LockedOut : PasswordCheckStatus.Failed);
        }

        if (user.AccessFailedCount > 0)
        {
            await users.ResetAccessFailedCountAsync(user);
        }

        // Also stores a re-hashed password when the hashing parameters were upgraded.
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return user.EmailConfirmed
            ? new PasswordCheck(PasswordCheckStatus.Succeeded, user.Id.ToString())
            : new PasswordCheck(PasswordCheckStatus.EmailNotConfirmed);
    }

    public async Task<bool> ConfirmEmailAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        user.EmailConfirmed = true;
        ThrowIfFailed(await users.UpdateAsync(user));
        return true;
    }

    public async Task<string?> FindConfirmedUserIdAsync(string email, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        return user is { EmailConfirmed: true } ? user.Id.ToString() : null;
    }

    public async Task<bool> ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken)
    {
        var user = await FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        ThrowIfFailed(await users.RemovePasswordAsync(user));
        ThrowIfFailed(await users.AddPasswordAsync(user, newPassword));
        ThrowIfFailed(await users.SetLockoutEndDateAsync(user, null));
        ThrowIfFailed(await users.ResetAccessFailedCountAsync(user));
        return true;
    }

    public async Task<bool> CanSignInAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await FindByIdAsync(userId);
        return user is { EmailConfirmed: true } && !await users.IsLockedOutAsync(user);
    }

    // Subjects of other issuers are not GUIDs; they have no local account.
    private Task<ApplicationUser?> FindByIdAsync(string userId) =>
        Guid.TryParse(userId, out _) ? users.FindByIdAsync(userId) : Task.FromResult<ApplicationUser?>(null);

    // Identity's messages describe password rules and are safe to show to the caller.
    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new DomainException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }
}
