using Boilerplate.Domain.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Configurations;

/// <summary>File descriptions (ADR-0030); the content lives in file storage. Tenant-owned: filtered, stamped and indexed by tenant.</summary>
internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.HasKey(file => file.Id);
        builder.Property(file => file.FileName).HasMaxLength(StoredFile.FileNameMaxLength);
        builder.Property(file => file.ContentType).HasMaxLength(StoredFile.ContentTypeMaxLength);

        // Derived from the id; never stored.
        builder.Ignore(file => file.StorageKey);

        // The list endpoint reads one tenant's files, newest first; also serves the tenant filter.
        builder.HasIndex(file => new { file.TenantId, file.CreatedAt });
    }
}
