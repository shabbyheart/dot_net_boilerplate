using System.ComponentModel.DataAnnotations;

namespace Boilerplate.Infrastructure.Persistence;

/// <summary>Database settings, bound from the <c>Database</c> section and validated at startup (ADR-0007, ADR-0011).</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Npgsql connection string. A secret: supply it through user secrets or the
    /// <c>Database__ConnectionString</c> environment variable, never through appsettings files.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
