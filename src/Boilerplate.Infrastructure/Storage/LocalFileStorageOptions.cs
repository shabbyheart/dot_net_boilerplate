namespace Boilerplate.Infrastructure.Storage;

/// <summary>Local disk storage (<c>Storage:Local</c> section; ADR-0030).</summary>
public sealed class LocalFileStorageOptions
{
    public const string SectionName = "Storage:Local";

    /// <summary>
    /// The directory that holds file content; created if missing. Unset means file storage is not configured, and
    /// uploads and downloads answer 409. Several instances must share it (a volume).
    /// </summary>
    public string? RootPath { get; set; }
}
