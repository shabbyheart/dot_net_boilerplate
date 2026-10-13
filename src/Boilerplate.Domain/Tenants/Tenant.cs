using Boilerplate.Domain.Common;

namespace Boilerplate.Domain.Tenants;

/// <summary>An isolated customer of the system. All tenant-owned data references one tenant (ADR-0010).</summary>
public sealed class Tenant : Entity, IAuditable
{
    public const int NameMaxLength = 200;

    private Tenant(string name)
    {
        Name = name;
    }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Tenant Create(string name) => new(NormalizeName(name));

    public void Rename(string name) => Name = NormalizeName(name);

    private static string NormalizeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException("Tenant name must not be empty.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException($"Tenant name must be at most {NameMaxLength} characters.");
        }

        return trimmed;
    }
}
