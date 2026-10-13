using Boilerplate.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Name).HasMaxLength(Project.NameMaxLength);

        // Bounded: descriptions are short summaries, not documents.
        builder.Property(project => project.Description).HasMaxLength(Project.DescriptionMaxLength);

        // Names are unique per tenant (exact, case-sensitive comparison); also serves listing ordered by name.
        builder.HasIndex(project => new { project.TenantId, project.Name }).IsUnique();
    }
}
