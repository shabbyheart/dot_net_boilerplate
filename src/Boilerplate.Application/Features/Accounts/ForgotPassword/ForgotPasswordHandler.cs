using Boilerplate.Application.Common.Messaging;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Domain.Accounts;

namespace Boilerplate.Application.Features.Accounts.ForgotPassword;

public sealed class ForgotPasswordHandler(
    IAppDbContext db,
    IUserAccounts accounts,
    OneTimeTokens oneTimeTokens,
    Outbox outbox,
    AccountSettings settings)
{
    /// <summary>
    /// E-mails a reset link if the address has a confirmed account. The response is the same either way, so callers
    /// cannot discover registered addresses (ADR-0025).
    /// </summary>
    public async Task HandleAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = await accounts.FindConfirmedUserIdAsync(request.Email, cancellationToken);
        if (userId is null)
        {
            return;
        }

        var token = await oneTimeTokens.IssueAsync(
            userId, OneTimeTokenPurpose.PasswordReset, settings.PasswordResetLifetime, cancellationToken);
        outbox.Enqueue(AccountEmails.ResetPassword(request.Email, settings.PasswordResetUrl!, token, settings.PasswordResetLifetime));

        await db.SaveChangesAsync(cancellationToken);
    }
}
