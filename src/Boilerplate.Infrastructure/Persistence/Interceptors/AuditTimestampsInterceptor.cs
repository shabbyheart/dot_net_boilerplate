using Boilerplate.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Boilerplate.Infrastructure.Persistence.Interceptors;

/// <summary>Sets <see cref="IAuditable"/> timestamps from <see cref="TimeProvider"/> on every save (ADR-0012).</summary>
internal sealed class AuditTimestampsInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            var createdAt = entry.Property<DateTimeOffset>(nameof(IAuditable.CreatedAt));
            var updatedAt = entry.Property<DateTimeOffset>(nameof(IAuditable.UpdatedAt));

            switch (entry.State)
            {
                case EntityState.Added:
                    createdAt.CurrentValue = now;
                    updatedAt.CurrentValue = now;
                    break;

                case EntityState.Modified:
                    updatedAt.CurrentValue = now;
                    // The creation time never changes after insert.
                    createdAt.IsModified = false;
                    break;
            }
        }
    }
}
