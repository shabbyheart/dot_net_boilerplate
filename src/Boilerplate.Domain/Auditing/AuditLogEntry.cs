using Boilerplate.Domain.Common;

namespace Boilerplate.Domain.Auditing;

/// <summary>
/// A record of a security-relevant change in one tenant: who did what to which entity, and when (ADR-0028).
/// Append-only: nothing changes an entry once recorded; only retention cleanup removes it.
/// </summary>
public sealed class AuditLogEntry : Entity, ITenantOwned
{
    public const int ActionMaxLength = 64;
    public const int DescriptionMaxLength = 1000;

    private AuditLogEntry(DateTimeOffset occurredAt, string? actorId, string action, Guid targetId, string description)
    {
        OccurredAt = occurredAt;
        ActorId = actorId;
        Action = action;
        TargetId = targetId;
        Description = description;
    }

    public Guid TenantId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>The token subject of the user who made the change, or <see langword="null"/> for commands.</summary>
    public string? ActorId { get; private set; }

    /// <summary>A stable name such as <c>role.created</c>.</summary>
    public string Action { get; private set; }

    /// <summary>The id of the affected entity (role, invitation, tenant).</summary>
    public Guid TargetId { get; private set; }

    /// <summary>What happened, for people. Shortened to <see cref="DescriptionMaxLength"/> characters.</summary>
    public string Description { get; private set; }

    public static AuditLogEntry Record(
        DateTimeOffset occurredAt,
        string? actorId,
        string action,
        Guid targetId,
        string description)
    {
        if (string.IsNullOrWhiteSpace(action) || action.Length > ActionMaxLength)
        {
            throw new DomainException($"An audit log action needs 1 to {ActionMaxLength} characters.");
        }

        if (actorId is not null && (actorId.Length == 0 || actorId.Length > IAuditable.UserIdMaxLength))
        {
            throw new DomainException($"An actor id needs 1 to {IAuditable.UserIdMaxLength} characters.");
        }

        if (targetId == Guid.Empty)
        {
            throw new DomainException("An audit log entry needs a target id.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new AuditLogEntry(occurredAt, actorId, action, targetId, Shorten(description));
    }

    // Descriptions list names chosen by users; a long list must not make the change itself fail.
    private static string Shorten(string description) =>
        description.Length <= DescriptionMaxLength
            ? description
            : string.Concat(description.AsSpan(0, DescriptionMaxLength - 1), "…");
}
