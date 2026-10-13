using System.Reflection;
using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Messaging;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Domain.Accounts;
using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Common;
using Boilerplate.Domain.Projects;
using Boilerplate.Domain.Tenants;
using Boilerplate.Infrastructure.Accounts;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Boilerplate.Infrastructure.Persistence;

/// <summary>
/// The application's EF Core context (ADR-0005). Applies the tenancy rules to every
/// <see cref="ITenantOwned"/> entity (ADR-0010) so no feature has to remember them.
/// Also holds Identity's user tables for local accounts (ADR-0025).
/// </summary>
public class AppDbContext : IdentityUserContext<ApplicationUser, Guid>, IAppDbContext
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

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<OneTimeToken> OneTimeTokens => Set<OneTimeToken>();

    // Read by the tenant query filter. EF Core evaluates it for each query against the current context instance.
    private Guid? CurrentTenantId => _tenantContext.TenantId;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw ConcurrencyConflict(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw UniqueConflict(exception);
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw ConcurrencyConflict(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw UniqueConflict(exception);
        }
    }

    /// <remarks>
    /// Derived contexts must add their own entities before calling this method,
    /// so that the conventions below apply to them too.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's model first, so the configurations below can rename its tables and bound its strings.
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        var rootEntityTypes = builder.Model.GetEntityTypes()
            .Where(entityType => !entityType.IsOwned() && entityType.BaseType is null)
            .Select(entityType => entityType.ClrType)
            .ToList();

        foreach (var clrType in rootEntityTypes)
        {
            // Ids are created by the domain (GUID v7, ADR-0012), never by the database.
            if (typeof(Entity).IsAssignableFrom(clrType))
            {
                builder.Entity(clrType).Property(nameof(Entity.Id)).ValueGeneratedNever();
            }

            if (typeof(IAuditable).IsAssignableFrom(clrType))
            {
                builder.Entity(clrType).Property(nameof(IAuditable.CreatedBy)).HasMaxLength(IAuditable.UserIdMaxLength);
                builder.Entity(clrType).Property(nameof(IAuditable.UpdatedBy)).HasMaxLength(IAuditable.UserIdMaxLength);
            }

            if (typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                ApplyTenantRulesMethod.MakeGenericMethod(clrType).Invoke(this, [builder]);
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

    // Handlers check uniqueness first for a specific message; this catches the race between check and insert.
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    // A row with a concurrency token (such as a refresh token) was changed by another request since it was read.
    private static ConflictException ConcurrencyConflict(DbUpdateConcurrencyException exception) =>
        new("The data was changed by another request. Retry with the current state.", exception);

    // The constraint name is not exposed: it would reveal schema details to clients.
    private static ConflictException UniqueConflict(DbUpdateException exception) =>
        new("The change conflicts with existing data: a value that must be unique is already in use.", exception);
}
