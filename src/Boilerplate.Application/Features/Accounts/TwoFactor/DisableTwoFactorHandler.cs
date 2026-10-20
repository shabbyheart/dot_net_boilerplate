using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Messaging;
using Boilerplate.Application.Common.Persistence;

namespace Boilerplate.Application.Features.Accounts.TwoFactor;

public sealed class DisableTwoFactorHandler(
    IAppDbContext db,
    IUserAccounts accounts,
    CurrentAccount currentAccount,
    Outbox outbox)
{
    /// <summary>Switches two-factor sign-in off; needs the password and a code or recovery code (ADR-0031).</summary>
    /// <exception cref="NotFoundException">The caller has no local account.</exception>
    /// <exception cref="ConflictException">Two-factor sign-in is off.</exception>
    /// <exception cref="ForbiddenException">Wrong password or code, or the account is locked.</exception>
    public async Task HandleAsync(DisableTwoFactorRequest request, CancellationToken cancellationToken)
    {
        var (userId, account) = await currentAccount.GetAsync(cancellationToken);
        if (!account.Enabled)
        {
            throw new ConflictException("Two-factor sign-in is off.");
        }

        await currentAccount.EnsureCredentialsAsync(
            userId, request.Password, SecondFactor.From(request.Code, request.RecoveryCode), cancellationToken);

        await accounts.DisableTwoFactorAsync(userId, cancellationToken);
        outbox.Enqueue(AccountEmails.TwoFactorDisabled(account.Email));
        await db.SaveChangesAsync(cancellationToken);
    }
}
