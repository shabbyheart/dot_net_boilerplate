using Boilerplate.Application.Common.Pagination;

namespace Boilerplate.Application.Features.Auditing.ListAuditLog;

/// <summary>Lists the current tenant's audit log, newest first. Every filter is optional.</summary>
public sealed record ListAuditLogRequest : PageRequest
{
    /// <summary>Only entries with this action, such as <c>role.assigned</c>.</summary>
    public string? Action { get; init; }

    /// <summary>Only changes made by this user.</summary>
    public string? ActorId { get; init; }

    /// <summary>Only changes to this role, invitation or tenant.</summary>
    public Guid? TargetId { get; init; }
}
