using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Configurations;

/// <summary>Invitations to a tenant role (ADR-0027). Tenant-owned: filtered, stamped and indexed by tenant.</summary>
internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Email).HasMaxLength(Invitation.EmailMaxLength);
        builder.Property(invitation => invitation.AcceptedBy).HasMaxLength(IAuditable.UserIdMaxLength);

        // Hex SHA-256.
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(64);

        // Deleting a role deletes its invitations, as it deletes its assignments.
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(invitation => invitation.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Acceptance looks up the presented token across tenants.
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();

        // Revoking earlier invitations of an address to a role; also serves the tenant filter.
        builder.HasIndex(invitation => new { invitation.TenantId, invitation.Email, invitation.RoleId });

        // Retention cleanup of expired invitations (ADR-0026).
        builder.HasIndex(invitation => invitation.ExpiresAt);

        // PostgreSQL's xmin as a concurrency token: an invitation cannot be accepted twice by concurrent requests.
        builder.Property<uint>("Version").IsRowVersion();
    }
}
