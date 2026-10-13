using Boilerplate.Application.Common.Persistence;
using Boilerplate.Domain.Accounts;
using Boilerplate.Domain.Common;

namespace Boilerplate.Application.Features.Accounts.ResetPassword;

public sealed class ResetPasswordHandler(
    IAppDbContext db,
    IUserAccounts accounts,
    OneTimeTokens oneTimeTokens,
    AccountSessions sessions)
{
    /// <summary>Sets the new password, ends any lockout and signs the user out of every session.</summary>
    /// <exception cref="DomainException">The link is unknown, used or expired, or the password breaks the password rules.</exception>
    public async Task HandleAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var token = await oneTimeTokens.RedeemAsync(request.Token, OneTimeTokenPurpose.PasswordReset, cancellationToken);

        if (!await accounts.ResetPasswordAsync(token.UserId, request.NewPassword, cancellationToken))
        {
            throw new DomainException(OneTimeToken.InvalidMessage);
        }

        await sessions.RevokeAllAsync(token.UserId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
