namespace Boilerplate.Application.Common.Messaging;

/// <summary>
/// Delivers one type of outbox message (ADR-0019). Called at least once per message, so implementations must be
/// idempotent; use <see cref="OutboxMessageContext.MessageId"/> to recognise repeats. Throwing schedules a retry.
/// </summary>
public interface IOutboxMessageHandler<in TMessage>
{
    Task HandleAsync(TMessage message, OutboxMessageContext context, CancellationToken cancellationToken);
}
