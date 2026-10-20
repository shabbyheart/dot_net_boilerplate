using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Auditing.ListAuditLog;

public sealed class ListAuditLogHandler(IAppDbContext db)
{
    public Task<PagedResponse<AuditLogEntryResponse>> HandleAsync(ListAuditLogRequest request, CancellationToken cancellationToken)
    {
        var entries = db.AuditLogEntries.AsNoTracking();

        if (!string.IsNullOrEmpty(request.Action))
        {
            entries = entries.Where(entry => entry.Action == request.Action);
        }

        if (!string.IsNullOrEmpty(request.ActorId))
        {
            entries = entries.Where(entry => entry.ActorId == request.ActorId);
        }

        if (request.TargetId is { } targetId)
        {
            entries = entries.Where(entry => entry.TargetId == targetId);
        }

        return entries
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Select(AuditLogEntryResponse.FromEntry)
            .ToPagedResponseAsync(request, cancellationToken);
    }
}
