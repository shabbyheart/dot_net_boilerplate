using Boilerplate.Domain.Accounts;
using Boilerplate.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Configurations;

/// <summary>E-mailed single-use tokens (ADR-0025). Not tenant-owned: accounts exist outside tenants.</summary>
internal sealed class OneTimeTokenConfiguration : IEntityTypeConfiguration<OneTimeToken>
{
    public void Configure(EntityTypeBuilder<OneTimeToken> builder)
    {
        builder.HasKey(token => token.Id);
        builder.Property(token => token.UserId).HasMaxLength(IAuditable.UserIdMaxLength);
        builder.Property(token => token.Purpose).HasConversion<string>().HasMaxLength(32);

        // Hex SHA-256.
        builder.Property(token => token.TokenHash).HasMaxLength(64);

        // Lookup by the presented token; invalidating a user's earlier tokens when a new one is sent.
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.UserId, token.Purpose });

        // PostgreSQL's xmin as a concurrency token: a token cannot be redeemed twice by concurrent requests.
        builder.Property<uint>("Version").IsRowVersion();
    }
}
