using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Configurations;

internal sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.UserId).HasMaxLength(IAuditable.UserIdMaxLength);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // One assignment per user and role; also serves the permission check, which filters by tenant and user.
        builder.HasIndex(assignment => new { assignment.TenantId, assignment.UserId, assignment.RoleId }).IsUnique();
    }
}
