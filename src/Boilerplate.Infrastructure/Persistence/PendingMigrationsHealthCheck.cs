using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Boilerplate.Infrastructure.Persistence;

/// <summary>
/// Unhealthy while the database lacks migrations the code expects, so an instance gets no traffic before the
/// schema matches (ADR-0024). Migrations themselves are applied as a separate step (ADR-0011).
/// </summary>
internal sealed class PendingMigrationsHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        return pending.Count == 0
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus, $"{pending.Count} database migration(s) not applied.");
    }
}
