using Boilerplate.Application.Common.Messaging;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Domain.Accounts;

namespace Boilerplate.Application.Features.Accounts.Register;

public sealed class RegisterHandler(
    IAppDbContext db,
    IUserAccounts accounts,
    OneTimeTokens oneTimeTokens,
    Outbox outbox,
    AccountSettings settings)
{
    /// <summary>
    /// Registers the address and e-mails it. The response is the same whether or not the address already has an
    /// account, so callers cannot discover registered addresses (ADR-0025).
    /// </summary>
    public async Task HandleAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var registration = await accounts.RegisterAsync(request.Email, request.Password, cancellationToken);

        if (registration.AlreadyConfirmed)
        {
            outbox.Enqueue(AccountEmails.AlreadyRegistered(request.Email));
        }
        else
        {
            // A new token replaces any earlier one, so only the latest registration (and its password) can be confirmed.
            var token = await oneTimeTokens.IssueAsync(
                registration.UserId, OneTimeTokenPurpose.EmailConfirmation, settings.EmailConfirmationLifetime, cancellationToken);
            outbox.Enqueue(AccountEmails.ConfirmEmail(
                request.Email, settings.EmailConfirmationUrl!, token, settings.EmailConfirmationLifetime));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
