using System.Reflection;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Common;
using Boilerplate.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Infrastructure.Persistence;

/// <summary>
/// The application's EF Core context (ADR-0005). Applies the tenancy rules to every
/// <see cref="ITenantOwned"/> entity (ADR-0010) so no feature has to remember them.
/// </summary>
public class AppDbContext : DbContext, IAppDbContext
{
    /// <summary>Name of the tenant query filter, for the rare reviewed code that must bypass it.</summary>
    public const string TenantQueryFilter = "Tenant";

    private static readonly MethodInfo ApplyTenantRulesMethod = typeof(AppDbContext)
        .GetMethod(nameof(ApplyTenantRules), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : this((DbContextOptions)options, tenantContext)
    {
    }

    /// <summary>For contexts that extend the model, such as test contexts with probe entities.</summary>
    protected AppDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    // Read by the tenant query filter. EF Core evaluates it for each query against the current context instance.
    private Guid? CurrentTenantId => _tenantContext.TenantId;

    /// <remarks>
    /// Derived contexts must add their own entities before calling this method,
    /// so that the conventions below apply to them too.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        var rootEntityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(entityType => !entityType.IsOwned() && entityType.BaseType is null)
            .Select(entityType => entityType.ClrType)
            .ToList();

        foreach (var clrType in rootEntityTypes)
        {
            // Ids are created by the domain (GUID v7, ADR-0012), never by the database.
            if (typeof(Entity).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).Property(nameof(Entity.Id)).ValueGeneratedNever();
            }

            if (typeof(IAuditable).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).Property(nameof(IAuditable.CreatedBy)).HasMaxLength(IAuditable.UserIdMaxLength);
                modelBuilder.Entity(clrType).Property(nameof(IAuditable.UpdatedBy)).HasMaxLength(IAuditable.UserIdMaxLength);
            }

            if (typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                ApplyTenantRulesMethod.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplyTenantRules<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        var entity = modelBuilder.Entity<TEntity>();

        // No current tenant compares against null and matches no rows: fail closed.
        entity.HasQueryFilter(TenantQueryFilter, row => row.TenantId == CurrentTenantId);

        entity.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(row => row.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Every query on tenant-owned data filters by tenant_id. An index that already starts with tenant_id
        // (such as a per-tenant unique index) serves those queries, so only add one when none exists.
        var hasTenantLeadingIndex = entity.Metadata.GetIndexes()
            .Any(index => index.Properties[0].Name == nameof(ITenantOwned.TenantId));
        if (!hasTenantLeadingIndex)
        {
            entity.HasIndex(row => row.TenantId);
        }
    }
}
