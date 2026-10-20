using System.Linq.Expressions;
using Boilerplate.Domain.Files;

namespace Boilerplate.Application.Features.Files;

/// <summary>A stored file's description as returned by the API; the content is downloaded separately.</summary>
/// <param name="Length">The content length in bytes.</param>
/// <param name="CreatedBy">Who uploaded it.</param>
public sealed record FileResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long Length,
    DateTimeOffset CreatedAt,
    string? CreatedBy)
{
    /// <summary>Hand-written mapping (ADR-0015); EF Core translates it to a column projection in queries.</summary>
    public static readonly Expression<Func<StoredFile, FileResponse>> FromFile = file => new FileResponse(
        file.Id,
        file.FileName,
        file.ContentType,
        file.Length,
        file.CreatedAt,
        file.CreatedBy);

    private static readonly Func<StoredFile, FileResponse> MapFile = FromFile.Compile();

    /// <summary>Maps an entity already in memory (after a save), with the same mapping as queries.</summary>
    public static FileResponse From(StoredFile file) => MapFile(file);
}
