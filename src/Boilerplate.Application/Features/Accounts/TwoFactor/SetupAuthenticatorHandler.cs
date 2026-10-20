using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;

namespace Boilerplate.Application.Features.Accounts.TwoFactor;

public sealed class SetupAuthenticatorHandler(
    IAppDbContext db,
    IUserAccounts accounts,
    CurrentAccount currentAccount,
    AccountSettings settings)
{
    /// <summary>Creates a new authenticator key; two-factor sign-in stays off until it is enabled (ADR-0031).</summary>
    /// <exception cref="NotFoundException">The caller has no local account.</exception>
    /// <exception cref="ConflictException">Two-factor sign-in is on; replacing its key would lock the user out.</exception>
    public async Task<AuthenticatorSetupResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var (userId, account) = await currentAccount.GetAsync(cancellationToken);
        if (account.Enabled)
        {
            throw new ConflictException("Two-factor sign-in is on. Turn it off before setting up a new authenticator.");
        }

        var key = await accounts.ResetAuthenticatorKeyAsync(userId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new AuthenticatorSetupResponse(key, AuthenticatorUri(settings.TwoFactorIssuer, account.Email, key));
    }

    // Key URI format understood by authenticator apps: otpauth://totp/Issuer:account?secret=...&issuer=Issuer
    private static string AuthenticatorUri(string issuer, string email, string key)
    {
        var escapedIssuer = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{escapedIssuer}:{Uri.EscapeDataString(email)}?secret={key}&issuer={escapedIssuer}&digits=6";
    }
}
