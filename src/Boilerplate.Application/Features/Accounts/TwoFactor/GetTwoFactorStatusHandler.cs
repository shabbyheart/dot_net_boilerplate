using Boilerplate.Application.Common.Exceptions;

namespace Boilerplate.Application.Features.Accounts.TwoFactor;

public sealed class GetTwoFactorStatusHandler(CurrentAccount currentAccount)
{
    /// <exception cref="NotFoundException">The caller has no local account.</exception>
    public async Task<TwoFactorStatusResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var (_, account) = await currentAccount.GetAsync(cancellationToken);
        return new TwoFactorStatusResponse(account.Enabled, account.RecoveryCodesLeft);
    }
}
