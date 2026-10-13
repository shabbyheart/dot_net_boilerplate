using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.IntegrationTests.Persistence;

/// <summary>The production model plus <see cref="TestNote"/>; all AppDbContext conventions apply to it.</summary>
internal sealed class IsolationTestDbContext(DbContextOptions<IsolationTestDbContext> options, ITenantContext tenantContext)
    : AppDbContext(options, tenantContext)
{
    public DbSet<TestNote> Notes => Set<TestNote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Added before base.OnModelCreating so the tenancy conventions apply to it.
        builder.Entity<TestNote>(note =>
        {
            note.Property(n => n.Title).HasMaxLength(TestNote.TitleMaxLength);

            // Unique per tenant, the pattern for tenant-owned unique values (ADR-0010).
            note.HasIndex(n => new { n.TenantId, n.Title }).IsUnique();
        });

        base.OnModelCreating(builder);
    }
}
