using System.ComponentModel.DataAnnotations;

namespace Boilerplate.Application.Features.Files;

/// <summary>Upload limits (<c>Storage</c> section, all optional; ADR-0030).</summary>
public sealed class FileSettings : IValidatableObject
{
    public const string SectionName = "Storage";

    /// <summary>The largest file that may be uploaded; the upload endpoint accepts bodies of this size plus overhead.</summary>
    public const long MaxFileSizeLimit = 100L * 1024 * 1024;

    private static readonly string[] DefaultAllowedContentTypes =
    [
        "application/pdf",
        "image/gif",
        "image/jpeg",
        "image/png",
        "image/webp",
        "text/csv",
        "text/plain",
    ];

    /// <summary>The largest file in bytes.</summary>
    [Range(1, MaxFileSizeLimit)]
    public long MaxFileSize { get; set; } = 10L * 1024 * 1024;

    /// <summary>
    /// Declared media types that may be uploaded. Unset means the defaults; configuring the list replaces them.
    /// </summary>
    // Configuration binding appends to a pre-filled collection, so the defaults are applied when nothing is bound.
    public string[]? AllowedContentTypes { get; set; }

    public IReadOnlyCollection<string> EffectiveAllowedContentTypes => AllowedContentTypes ?? DefaultAllowedContentTypes;

    public bool Allows(string contentType) =>
        EffectiveAllowedContentTypes.Contains(contentType.Trim(), StringComparer.OrdinalIgnoreCase);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AllowedContentTypes is { Length: 0 } || AllowedContentTypes?.Any(string.IsNullOrWhiteSpace) == true)
        {
            yield return new ValidationResult(
                $"{SectionName}:{nameof(AllowedContentTypes)} must list at least one media type and no empty entries.",
                [nameof(AllowedContentTypes)]);
        }
    }
}
