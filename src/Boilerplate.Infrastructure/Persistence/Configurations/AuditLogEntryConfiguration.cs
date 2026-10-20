using Boilerplate.Domain.Auditing;
using Boilerplate.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Configurations;

/// <summary>The audit log (ADR-0028). Tenant-owned: filtered, stamped and indexed by tenant.</summary>
internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Action).HasMaxLength(AuditLogEntry.ActionMaxLength);
        builder.Property(entry => entry.ActorId).HasMaxLength(IAuditable.UserIdMaxLength);
        builder.Property(entry => entry.Description).HasMaxLength(AuditLogEntry.DescriptionMaxLength);

        // No foreign key to the target: entries outlive the roles and invitations they describe.

        // The list endpoint reads one tenant's entries, newest first; also serves the tenant filter.
        builder.HasIndex(entry => new { entry.TenantId, entry.OccurredAt });

        // Retention cleanup across tenants (ADR-0026).
        builder.HasIndex(entry => entry.OccurredAt);
    }
}
