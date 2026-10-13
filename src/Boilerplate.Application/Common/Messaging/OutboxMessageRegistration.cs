namespace Boilerplate.Application.Common.Messaging;

/// <summary>A registered message type: its stable name, CLR type, and how to deserialize and handle it.</summary>
public sealed record OutboxMessageRegistration(
    string Name,
    Type MessageType,
    Func<IServiceProvider, string, OutboxMessageContext, CancellationToken, Task> HandleAsync);
