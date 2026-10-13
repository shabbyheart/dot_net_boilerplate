using Boilerplate.Application.Common.Persistence;
using Boilerplate.Domain.Accounts;
using Boilerplate.Domain.Common;

namespace Boilerplate.Application.Features.Accounts.ConfirmEmail;

public sealed class ConfirmEmailHandler(IAppDbContext db, IUserAccounts accounts, OneTimeTokens oneTimeTokens)
{
    /// <exception cref="DomainException">The link is unknown, used or expired.</exception>
    public async Task HandleAsync(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        var token = await oneTimeTokens.RedeemAsync(request.Token, OneTimeTokenPurpose.EmailConfirmation, cancellationToken);

        if (!await accounts.ConfirmEmailAsync(token.UserId, cancellationToken))
        {
            throw new DomainException(OneTimeToken.InvalidMessage);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
