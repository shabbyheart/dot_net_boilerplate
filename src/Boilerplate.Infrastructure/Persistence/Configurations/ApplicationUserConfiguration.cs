using Boilerplate.Infrastructure.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Configurations;

/// <summary>Identity's user table (ADR-0025), on top of Identity's own model configuration.</summary>
internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        // Identity limits user name and e-mail to 256 characters; these are its other strings.
        builder.Property(user => user.PasswordHash).HasColumnType("text");
        builder.Property(user => user.SecurityStamp).HasMaxLength(64);
        builder.Property(user => user.ConcurrencyStamp).HasMaxLength(64);
        builder.Property(user => user.PhoneNumber).HasMaxLength(32);

        builder.HasIndex(user => user.NormalizedUserName).HasDatabaseName("ix_users_normalized_user_name");
        builder.HasIndex(user => user.NormalizedEmail).HasDatabaseName("ix_users_normalized_email");
    }
}
