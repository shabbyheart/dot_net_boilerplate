namespace Boilerplate.Application.Common.Messaging;

/// <summary>All registered outbox message types, by name and by CLR type.</summary>
public sealed class OutboxMessageTypes
{
    private readonly Dictionary<string, OutboxMessageRegistration> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, OutboxMessageRegistration> _byType = [];

    public OutboxMessageTypes(IEnumerable<OutboxMessageRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            if (!_byName.TryAdd(registration.Name, registration) || !_byType.TryAdd(registration.MessageType, registration))
            {
                throw new InvalidOperationException(
                    $"Outbox message '{registration.Name}' ({registration.MessageType.Name}) is registered more than once.");
            }
        }
    }

    /// <exception cref="InvalidOperationException">The type was never registered with AddOutboxMessage.</exception>
    public string NameOf(Type messageType) =>
        _byType.TryGetValue(messageType, out var registration)
            ? registration.Name
            : throw new InvalidOperationException(
                $"{messageType.Name} is not a registered outbox message. Register it with AddOutboxMessage.");

    /// <summary>The registration for a stored type name, or <see langword="null"/> if unknown.</summary>
    public OutboxMessageRegistration? Find(string name) => _byName.GetValueOrDefault(name);
}
