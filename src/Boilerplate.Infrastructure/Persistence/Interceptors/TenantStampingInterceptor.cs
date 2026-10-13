using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Boilerplate.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Enforces tenant isolation on writes (ADR-0010): new rows get the current tenant, and rows of other
/// tenants can be neither inserted, updated nor deleted. Bulk operations (ExecuteUpdate/ExecuteDelete)
/// bypass interceptors; they are constrained by the tenant query filter instead.
/// </summary>
internal sealed class TenantStampingInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Enforce(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Enforce(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Enforce(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                EnforceEntry(entry, RequireCurrentTenant(entry));
            }
        }
    }

    private static void EnforceEntry(EntityEntry<ITenantOwned> entry, Guid currentTenantId)
    {
        var tenantId = entry.Property<Guid>(nameof(ITenantOwned.TenantId));

        if (entry.State == EntityState.Added)
        {
            if (tenantId.CurrentValue == Guid.Empty)
            {
                tenantId.CurrentValue = currentTenantId;
            }
            else if (tenantId.CurrentValue != currentTenantId)
            {
                throw Violation(entry, "cannot be created for another tenant");
            }

            return;
        }

        if (tenantId.OriginalValue != currentTenantId)
        {
            throw Violation(entry, "belongs to another tenant");
        }

        if (tenantId.CurrentValue != tenantId.OriginalValue)
        {
            throw Violation(entry, "cannot be moved to another tenant");
        }
    }

    private Guid RequireCurrentTenant(EntityEntry<ITenantOwned> entry) =>
        tenantContext.TenantId ?? throw Violation(entry, "requires a current tenant");

    private static InvalidOperationException Violation(EntityEntry<ITenantOwned> entry, string reason) =>
        new($"Tenant isolation: {entry.Metadata.ClrType.Name} {reason}.");
}
