using System.Globalization;
using Boilerplate.Domain.Common;

namespace Boilerplate.Domain.Files;

/// <summary>
/// A file uploaded to a tenant (ADR-0030). Only the description lives here; the content lives in file storage under
/// <see cref="StorageKey"/>, which is derived from the id so no user input reaches a storage path.
/// </summary>
public sealed class StoredFile : Entity, IAuditable, ITenantOwned
{
    public const int FileNameMaxLength = 255;
    public const int ContentTypeMaxLength = 100;

    /// <summary>Used when nothing usable is left of the uploaded name.</summary>
    public const string DefaultFileName = "file";

    private StoredFile(string fileName, string contentType, long length)
    {
        FileName = fileName;
        ContentType = contentType;
        Length = length;
    }

    /// <summary>The uploaded name without any directory part or control characters.</summary>
    public string FileName { get; private set; }

    /// <summary>The declared media type, in lower case.</summary>
    public string ContentType { get; private set; }

    /// <summary>The content length in bytes.</summary>
    public long Length { get; private set; }

    /// <summary>Where the content is stored: the id as 32 hexadecimal digits.</summary>
    public string StorageKey => Id.ToString("N", CultureInfo.InvariantCulture);

    public Guid TenantId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public static StoredFile Create(string fileName, string contentType, long length)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        if (length <= 0)
        {
            throw new DomainException("A file must not be empty.");
        }

        var normalizedType = contentType.Trim().ToLowerInvariant();
        if (normalizedType.Length > ContentTypeMaxLength)
        {
            throw new DomainException($"Content type must be at most {ContentTypeMaxLength} characters.");
        }

        return new StoredFile(NormalizeFileName(fileName), normalizedType, length);
    }

    /// <summary>
    /// Keeps the last path segment (either separator), drops control characters and quotes, and shortens the rest,
    /// so the name is safe to show and to send back in a <c>Content-Disposition</c> header.
    /// </summary>
    public static string NormalizeFileName(string? fileName)
    {
        var name = fileName ?? string.Empty;
        var lastSeparator = name.LastIndexOfAny(['/', '\\']);
        if (lastSeparator >= 0)
        {
            name = name[(lastSeparator + 1)..];
        }

        name = string.Concat(name.Where(character => !char.IsControl(character) && character != '"')).Trim();
        if (name.Length == 0 || name is "." or "..")
        {
            return DefaultFileName;
        }

        if (name.Length <= FileNameMaxLength)
        {
            return name;
        }

        // Never cut a surrogate pair in half.
        var length = char.IsHighSurrogate(name[FileNameMaxLength - 1]) ? FileNameMaxLength - 1 : FileNameMaxLength;
        return name[..length];
    }
}
