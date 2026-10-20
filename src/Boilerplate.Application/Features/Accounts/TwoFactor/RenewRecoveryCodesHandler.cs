using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;

namespace Boilerplate.Application.Features.Accounts.TwoFactor;

public sealed class RenewRecoveryCodesHandler(IAppDbContext db, IUserAccounts accounts, CurrentAccount currentAccount)
{
    /// <summary>Replaces every recovery code; the old ones stop working (ADR-0031).</summary>
    /// <exception cref="NotFoundException">The caller has no local account.</exception>
    /// <exception cref="ConflictException">Two-factor sign-in is off.</exception>
    /// <exception cref="ForbiddenException">Wrong password or code, or the account is locked.</exception>
    public async Task<RecoveryCodesResponse> HandleAsync(RenewRecoveryCodesRequest request, CancellationToken cancellationToken)
    {
        var (userId, account) = await currentAccount.GetAsync(cancellationToken);
        if (!account.Enabled)
        {
            throw new ConflictException("Two-factor sign-in is off.");
        }

        await currentAccount.EnsureCredentialsAsync(userId, request.Password, new SecondFactor(request.Code, null), cancellationToken);

        var codes = RecoveryCodes.Create();
        await accounts.ReplaceRecoveryCodesAsync(userId, codes.Select(RecoveryCodes.Hash).ToList(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new RecoveryCodesResponse(codes);
    }
}
