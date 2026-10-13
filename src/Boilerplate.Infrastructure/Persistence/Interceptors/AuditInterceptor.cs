using Boilerplate.Application.Common.Security;
using Boilerplate.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Boilerplate.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Records when and by whom <see cref="IAuditable"/> entities were created and last updated (ADR-0012, ADR-0016).
/// Time comes from <see cref="TimeProvider"/>, the user from <see cref="ICurrentUser"/>.
/// </summary>
internal sealed class AuditInterceptor(TimeProvider timeProvider, ICurrentUser currentUser) : SaveChangesInterceptor
{
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

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

        // PostgreSQL stores microseconds; .NET has 100 ns ticks. Round down so the entity in memory (returned right
        // after a save) equals what a later read returns.
        var now = timeProvider.GetUtcNow();
        now = now.AddTicks(-(now.Ticks % TicksPerMicrosecond));
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            var createdAt = entry.Property<DateTimeOffset>(nameof(IAuditable.CreatedAt));
            var createdBy = entry.Property<string?>(nameof(IAuditable.CreatedBy));
            var updatedAt = entry.Property<DateTimeOffset>(nameof(IAuditable.UpdatedAt));
            var updatedBy = entry.Property<string?>(nameof(IAuditable.UpdatedBy));

            switch (entry.State)
            {
                case EntityState.Added:
                    createdAt.CurrentValue = now;
                    createdBy.CurrentValue = userId;
                    updatedAt.CurrentValue = now;
                    updatedBy.CurrentValue = userId;
                    break;

                case EntityState.Modified:
                    updatedAt.CurrentValue = now;
                    updatedBy.CurrentValue = userId;

                    // Creation details never change after insert.
                    createdAt.IsModified = false;
                    createdBy.IsModified = false;
                    break;
            }
        }
    }
}
