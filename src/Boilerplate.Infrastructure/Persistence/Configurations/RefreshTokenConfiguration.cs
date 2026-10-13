using Boilerplate.Domain.Accounts;
using Boilerplate.Domain.Common;
using Boilerplate.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Configurations;

/// <summary>
/// Not tenant-owned: refresh runs before any tenant is known, and a token is found only by its hash (ADR-0025).
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(token => token.Id);
        builder.Property(token => token.UserId).HasMaxLength(IAuditable.UserIdMaxLength);

        // Hex SHA-256.
        builder.Property(token => token.TokenHash).HasMaxLength(64);

        // Lookup by the presented token; family revocation; sign-out everywhere after a password reset.
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.FamilyId);
        builder.HasIndex(token => token.UserId);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(token => token.TenantId).OnDelete(DeleteBehavior.Restrict);

        // PostgreSQL's xmin as a concurrency token: two refreshes with the same token cannot both succeed.
        builder.Property<uint>("Version").IsRowVersion();
    }
}
