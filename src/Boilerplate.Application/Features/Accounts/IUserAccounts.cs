using Boilerplate.Domain.Common;

namespace Boilerplate.Application.Features.Accounts;

/// <summary>
/// User accounts: e-mail, password hash and lockout (ADR-0025). Implemented in Infrastructure with ASP.NET Core
/// Identity. Unless stated otherwise, methods only change the unit of work: the use case's
/// <c>SaveChangesAsync</c> commits them.
/// </summary>
public interface IUserAccounts
{
    /// <summary>
    /// Creates an account with an unconfirmed e-mail address. For an existing unconfirmed account the password is
    /// replaced, so only the latest registration can be confirmed. A confirmed account is left unchanged.
    /// </summary>
    /// <exception cref="DomainException">The password does not meet the password rules.</exception>
    Task<Registration> RegisterAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the password and, for accounts with two-factor sign-in, the second factor (ADR-0031). Counts failed
    /// attempts towards lockout and resets the count only when every factor succeeded. Saves the attempt counters (and
    /// a used recovery code) itself, because they must be stored even though the sign-in may still fail.
    /// </summary>
    Task<PasswordCheck> CheckPasswordAsync(
        string email,
        string password,
        SecondFactor? secondFactor,
        CancellationToken cancellationToken);

    /// <returns><see langword="false"/> when the user does not exist.</returns>
    Task<bool> ConfirmEmailAsync(string userId, CancellationToken cancellationToken);

    /// <returns>The user id, or <see langword="null"/> when no account with a confirmed address exists.</returns>
    Task<string?> FindConfirmedUserIdAsync(string email, CancellationToken cancellationToken);

    /// <summary>Sets a new password and ends any lockout.</summary>
    /// <returns><see langword="false"/> when the user does not exist.</returns>
    /// <exception cref="DomainException">The password does not meet the password rules.</exception>
    Task<bool> ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken);

    /// <summary>Whether the user exists, has a confirmed address and is not locked out.</summary>
    Task<bool> CanSignInAsync(string userId, CancellationToken cancellationToken);

    /// <returns>The account's two-factor state, or <see langword="null"/> when the user has no local account.</returns>
    Task<TwoFactorAccount?> FindTwoFactorAccountAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Re-authenticates a signed-in user before a sensitive change. A wrong password counts towards lockout and is
    /// saved immediately; a locked account always fails.
    /// </summary>
    Task<bool> VerifyPasswordAsync(string userId, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Checks an authenticator code against the current key (even before two-factor sign-in is on), or redeems a
    /// recovery code. A failure counts towards lockout and is saved immediately; a locked account always fails.
    /// </summary>
    Task<bool> VerifySecondFactorAsync(string userId, SecondFactor secondFactor, CancellationToken cancellationToken);

    /// <summary>Replaces the authenticator key with a new random one and returns it (base32).</summary>
    Task<string> ResetAuthenticatorKeyAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Switches two-factor sign-in on and stores the hashes of new recovery codes.</summary>
    Task EnableTwoFactorAsync(string userId, IReadOnlyCollection<string> recoveryCodeHashes, CancellationToken cancellationToken);

    /// <summary>Switches two-factor sign-in off, replaces the key with an unknown one and removes the recovery codes.</summary>
    Task DisableTwoFactorAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Replaces the recovery codes with new ones (hashes).</summary>
    Task ReplaceRecoveryCodesAsync(string userId, IReadOnlyCollection<string> recoveryCodeHashes, CancellationToken cancellationToken);
}
