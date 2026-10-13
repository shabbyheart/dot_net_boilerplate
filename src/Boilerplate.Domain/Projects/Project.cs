using Boilerplate.Domain.Common;

namespace Boilerplate.Domain.Projects;

/// <summary>
/// A tenant's project. This is the reference feature: a deliberately small, deletable example of how a
/// tenant-owned aggregate is modelled (docs/how-to-add-a-feature.md).
/// </summary>
public sealed class Project : Entity, IAuditable, ITenantOwned
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private Project(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public Guid TenantId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public static Project Create(string name, string? description) =>
        new(NormalizeName(name), NormalizeDescription(description));

    public void Update(string name, string? description)
    {
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
    }

    private static string NormalizeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException("Project name must not be empty.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException($"Project name must be at most {NameMaxLength} characters.");
        }

        return trimmed;
    }

    // An empty or whitespace-only description means "no description".
    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > DescriptionMaxLength)
        {
            throw new DomainException($"Project description must be at most {DescriptionMaxLength} characters.");
        }

        return trimmed;
    }
}
