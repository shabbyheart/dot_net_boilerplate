namespace Boilerplate.Application.Common.Messaging;

/// <summary>Delivery details passed to <see cref="IOutboxMessageHandler{TMessage}"/>.</summary>
/// <param name="MessageId">The outbox message id: the same on every attempt, so it identifies duplicates.</param>
/// <param name="TenantId">The tenant the message was recorded for; also set as the current tenant.</param>
/// <param name="Attempt">1 for the first attempt.</param>
public sealed record OutboxMessageContext(Guid MessageId, Guid? TenantId, int Attempt);
