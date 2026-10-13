using Boilerplate.Application.Common.Messaging;

namespace Boilerplate.Application.Common.Email;

/// <summary>Delivers <see cref="EmailMessage"/> outbox messages.</summary>
public sealed class SendEmailHandler(IEmailSender emailSender) : IOutboxMessageHandler<EmailMessage>
{
    public Task HandleAsync(EmailMessage message, OutboxMessageContext context, CancellationToken cancellationToken) =>
        emailSender.SendAsync(message, context.MessageId, cancellationToken);
}
