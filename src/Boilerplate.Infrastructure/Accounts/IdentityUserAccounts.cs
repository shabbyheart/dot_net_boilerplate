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
    IUserStore<ApplicationUser> userStore,
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

    public async Task<PasswordCheck> CheckPasswordAsync(
        string email,
        string password,
        SecondFactor? secondFactor,
        CancellationToken cancellationToken)
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
            return new PasswordCheck(await RecordFailureAsync(user, cancellationToken));
        }

        // The failure count is reset only when every factor succeeded (ADR-0031): resetting it after the password
        // alone would let anyone who knows the password guess codes without ever being locked out.
        if (user.EmailConfirmed && user.TwoFactorEnabled)
        {
            if (secondFactor is null)
            {
                await SaveIfChangedAsync(cancellationToken);
                return new PasswordCheck(PasswordCheckStatus.TwoFactorRequired);
            }

            if (!await IsSecondFactorValidAsync(user, secondFactor))
            {
                return new PasswordCheck(await RecordFailureAsync(user, cancellationToken));
            }
        }

        if (user.AccessFailedCount > 0)
        {
            await users.ResetAccessFailedCountAsync(user);
        }

        // Also stores a re-hashed password when the hashing parameters were upgraded, and a used recovery code.
        await SaveIfChangedAsync(cancellationToken);

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

    public async Task<TwoFactorAccount?> FindTwoFactorAccountAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await FindByIdAsync(userId);
        return user is null
            ? null
            : new TwoFactorAccount(user.Email!, user.TwoFactorEnabled, await users.CountRecoveryCodesAsync(user));
    }

    public async Task<bool> VerifyPasswordAsync(string userId, string password, CancellationToken cancellationToken)
    {
        var user = await FindByIdAsync(userId);
        if (user is null || await users.IsLockedOutAsync(user))
        {
            return false;
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            await RecordFailureAsync(user, cancellationToken);
            return false;
        }

        return true;
    }

    public async Task<bool> VerifySecondFactorAsync(string userId, SecondFactor secondFactor, CancellationToken cancellationToken)
    {
        var user = await FindByIdAsync(userId);
        if (user is null || await users.IsLockedOutAsync(user))
        {
            return false;
        }

        if (!await IsSecondFactorValidAsync(user, secondFactor))
        {
            await RecordFailureAsync(user, cancellationToken);
            return false;
        }

        if (user.AccessFailedCount > 0)
        {
            await users.ResetAccessFailedCountAsync(user);
        }

        return true;
    }

    public async Task<string> ResetAuthenticatorKeyAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId);
        ThrowIfFailed(await users.ResetAuthenticatorKeyAsync(user));
        return (await users.GetAuthenticatorKeyAsync(user))!;
    }

    public async Task EnableTwoFactorAsync(
        string userId,
        IReadOnlyCollection<string> recoveryCodeHashes,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId);
        await RecoveryCodeStore.ReplaceCodesAsync(user, recoveryCodeHashes, cancellationToken);
        ThrowIfFailed(await users.SetTwoFactorEnabledAsync(user, true));
    }

    public async Task DisableTwoFactorAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId);

        // A new random key that nobody has seen: the old authenticator stops working.
        ThrowIfFailed(await users.ResetAuthenticatorKeyAsync(user));
        await RecoveryCodeStore.ReplaceCodesAsync(user, [], cancellationToken);
        ThrowIfFailed(await users.SetTwoFactorEnabledAsync(user, false));
    }

    public async Task ReplaceRecoveryCodesAsync(
        string userId,
        IReadOnlyCollection<string> recoveryCodeHashes,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId);
        await RecoveryCodeStore.ReplaceCodesAsync(user, recoveryCodeHashes, cancellationToken);
    }

    // The recovery code store is the user store; UserManager offers no way to store codes it did not generate itself.
    private IUserTwoFactorRecoveryCodeStore<ApplicationUser> RecoveryCodeStore =>
        (IUserTwoFactorRecoveryCodeStore<ApplicationUser>)userStore;

    private async Task<bool> IsSecondFactorValidAsync(ApplicationUser user, SecondFactor secondFactor)
    {
        if (secondFactor.Code is { } code)
        {
            // Apps often show "123 456"; the provider expects the digits only.
            var digits = string.Concat(code.Where(char.IsAsciiDigit));
            return await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, digits);
        }

        // Codes are stored hashed, so the hash is what is looked up and removed.
        return secondFactor.RecoveryCodeHash is { } hash
            && (await users.RedeemTwoFactorRecoveryCodeAsync(user, hash)).Succeeded;
    }

    // Counts towards lockout and saves at once, because the request fails; locks the account when the limit is reached.
    private async Task<PasswordCheckStatus> RecordFailureAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await users.AccessFailedAsync(user);
        await db.SaveChangesAsync(cancellationToken);
        return await users.IsLockedOutAsync(user) ? PasswordCheckStatus.LockedOut : PasswordCheckStatus.Failed;
    }

    private async Task SaveIfChangedAsync(CancellationToken cancellationToken)
    {
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<ApplicationUser> RequireUserAsync(string userId) =>
        await FindByIdAsync(userId) ?? throw new InvalidOperationException("The account no longer exists.");

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
