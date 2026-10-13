using Boilerplate.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Name).HasMaxLength(Role.NameMaxLength);

        // Role names identify roles within a tenant; unique per tenant (ADR-0010).
        builder.HasIndex(role => new { role.TenantId, role.Name }).IsUnique();

        builder.OwnsMany(role => role.Permissions, permissions =>
        {
            permissions.ToTable("role_permissions");
            permissions.WithOwner().HasForeignKey("RoleId");
            permissions.Property(permission => permission.Name).HasMaxLength(PermissionName.MaxLength);

            // The key prevents duplicate grants and serves the permission check (role id, then permission name).
            permissions.HasKey("RoleId", nameof(PermissionGrant.Name));
        });
    }
}
