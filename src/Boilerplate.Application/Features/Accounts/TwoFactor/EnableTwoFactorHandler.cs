using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Messaging;
using Boilerplate.Application.Common.Persistence;

namespace Boilerplate.Application.Features.Accounts.TwoFactor;

public sealed class EnableTwoFactorHandler(
    IAppDbContext db,
    IUserAccounts accounts,
    CurrentAccount currentAccount,
    Outbox outbox)
{
    /// <summary>
    /// Switches two-factor sign-in on once the user proves the password and a working authenticator (ADR-0031).
    /// </summary>
    /// <exception cref="NotFoundException">The caller has no local account.</exception>
    /// <exception cref="ConflictException">Two-factor sign-in is already on.</exception>
    /// <exception cref="ForbiddenException">Wrong password or code, or the account is locked.</exception>
    public async Task<RecoveryCodesResponse> HandleAsync(EnableTwoFactorRequest request, CancellationToken cancellationToken)
    {
        var (userId, account) = await currentAccount.GetAsync(cancellationToken);
        if (account.Enabled)
        {
            throw new ConflictException("Two-factor sign-in is already on.");
        }

        await currentAccount.EnsureCredentialsAsync(userId, request.Password, new SecondFactor(request.Code, null), cancellationToken);

        var codes = RecoveryCodes.Create();
        await accounts.EnableTwoFactorAsync(userId, codes.Select(RecoveryCodes.Hash).ToList(), cancellationToken);
        outbox.Enqueue(AccountEmails.TwoFactorEnabled(account.Email));
        await db.SaveChangesAsync(cancellationToken);

        return new RecoveryCodesResponse(codes);
    }
}
