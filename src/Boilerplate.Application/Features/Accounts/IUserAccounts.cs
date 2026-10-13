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
    /// Checks the password and counts failed attempts towards lockout. Saves the attempt counters itself, because
    /// they must be stored even though the sign-in fails.
    /// </summary>
    Task<PasswordCheck> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken);

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
}
