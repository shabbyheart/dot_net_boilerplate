using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Security;

namespace Boilerplate.Application.Features.Accounts.TwoFactor;

/// <summary>The signed-in caller's local account, and re-authentication before two-factor changes (ADR-0031).</summary>
public sealed class CurrentAccount(ICurrentUser currentUser, IUserAccounts accounts)
{
    public const string CredentialsRejectedMessage =
        "The password or code is incorrect, or the account is temporarily locked.";

    /// <exception cref="NotFoundException">The caller has no local account (for example, an external provider's token).</exception>
    public async Task<(string UserId, TwoFactorAccount Account)> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Two-factor settings require an authenticated user.");
        var account = await accounts.FindTwoFactorAccountAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Account", userId);
        return (userId, account);
    }

    /// <summary>Checks the password and, when given, the second factor. Failures count towards lockout.</summary>
    /// <exception cref="ForbiddenException">The password or the second factor is wrong, or the account is locked.</exception>
    public async Task EnsureCredentialsAsync(
        string userId,
        string password,
        SecondFactor? secondFactor,
        CancellationToken cancellationToken)
    {
        if (!await accounts.VerifyPasswordAsync(userId, password, cancellationToken)
            || (secondFactor is not null && !await accounts.VerifySecondFactorAsync(userId, secondFactor, cancellationToken)))
        {
            throw new ForbiddenException(CredentialsRejectedMessage);
        }
    }
}
