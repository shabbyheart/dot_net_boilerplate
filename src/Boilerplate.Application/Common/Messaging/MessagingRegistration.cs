using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Application.Common.Messaging;

/// <summary>Registers the outbox and its message types explicitly (ADR-0004, ADR-0019).</summary>
public static class MessagingRegistration
{
    public static IServiceCollection AddOutbox(this IServiceCollection services)
    {
        services.AddScoped<Outbox>();
        services.AddSingleton<OutboxMessageTypes>();

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TMessage"/> under a stable <paramref name="name"/> with its handler.
    /// The name is stored with every message, so it must never change once messages exist.
    /// </summary>
    public static IServiceCollection AddOutboxMessage<TMessage, THandler>(this IServiceCollection services, string name)
        where TMessage : class
        where THandler : class, IOutboxMessageHandler<TMessage>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        services.AddScoped<IOutboxMessageHandler<TMessage>, THandler>();
        services.AddSingleton(new OutboxMessageRegistration(
            name,
            typeof(TMessage),
            async (scopedServices, payload, context, cancellationToken) =>
            {
                var message = JsonSerializer.Deserialize<TMessage>(payload, Outbox.SerializerOptions)
                    ?? throw new InvalidOperationException($"Outbox message '{name}' has an empty payload.");
                var handler = scopedServices.GetRequiredService<IOutboxMessageHandler<TMessage>>();
                await handler.HandleAsync(message, context, cancellationToken);
            }));

        return services;
    }
}
