using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Configurations;

/// <summary>Platform administrators (ADR-0027). Not tenant-owned.</summary>
internal sealed class PlatformAdministratorConfiguration : IEntityTypeConfiguration<PlatformAdministrator>
{
    public void Configure(EntityTypeBuilder<PlatformAdministrator> builder)
    {
        builder.HasKey(administrator => administrator.Id);
        builder.Property(administrator => administrator.UserId).HasMaxLength(IAuditable.UserIdMaxLength);

        // One grant per user; serves the check on every platform request.
        builder.HasIndex(administrator => administrator.UserId).IsUnique();
    }
}
