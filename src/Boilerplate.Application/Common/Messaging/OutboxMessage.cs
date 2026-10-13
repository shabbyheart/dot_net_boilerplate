using Boilerplate.Domain.Common;

namespace Boilerplate.Application.Common.Messaging;

/// <summary>
/// A side effect recorded in the same transaction as the business change and delivered later by the dispatcher
/// (ADR-0019). Not tenant-owned: the dispatcher reads every tenant's messages and acts for <see cref="TenantId"/>.
/// </summary>
public sealed class OutboxMessage : Entity
{
    public const int TypeMaxLength = 100;
    public const int LastErrorMaxLength = 2000;

    private OutboxMessage(string type, string payload, Guid? tenantId, DateTimeOffset createdAt)
    {
        Type = type;
        Payload = payload;
        TenantId = tenantId;
        CreatedAt = createdAt;
        AvailableAt = createdAt;
    }

    /// <summary>The registered message type name (stable, independent of CLR type names).</summary>
    public string Type { get; private set; }

    /// <summary>The message as JSON; cleared once delivered so its content is not retained.</summary>
    public string Payload { get; private set; }

    public Guid? TenantId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the message may next be claimed (also the end of a running lease).</summary>
    public DateTimeOffset AvailableAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public DateTimeOffset? DeadLetteredAt { get; private set; }

    internal static OutboxMessage Create(string type, string payload, Guid? tenantId, DateTimeOffset createdAt) =>
        new(type, payload, tenantId, createdAt);
}
