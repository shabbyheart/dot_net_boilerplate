using System.Linq.Expressions;
using Boilerplate.Domain.Auditing;

namespace Boilerplate.Application.Features.Auditing;

/// <summary>An audit log entry as returned by the API.</summary>
/// <param name="ActorId">The user who made the change; <see langword="null"/> for commands.</param>
/// <param name="Action">A stable name such as <c>role.created</c>.</param>
/// <param name="TargetId">The affected role, invitation or tenant.</param>
public sealed record AuditLogEntryResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string? ActorId,
    string Action,
    Guid TargetId,
    string Description)
{
    /// <summary>Hand-written mapping (ADR-0015), translated to SQL in queries.</summary>
    public static readonly Expression<Func<AuditLogEntry, AuditLogEntryResponse>> FromEntry = entry => new AuditLogEntryResponse(
        entry.Id,
        entry.OccurredAt,
        entry.ActorId,
        entry.Action,
        entry.TargetId,
        entry.Description);
}
