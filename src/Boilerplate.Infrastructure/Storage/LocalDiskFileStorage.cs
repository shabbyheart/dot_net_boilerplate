using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Storage;
using Microsoft.Extensions.Options;

namespace Boilerplate.Infrastructure.Storage;

/// <summary>
/// Stores file content as files under <see cref="LocalFileStorageOptions.RootPath"/> (ADR-0030). Content is written to
/// a temporary file and renamed, so a reader never sees half a file.
/// </summary>
internal sealed class LocalDiskFileStorage(IOptions<LocalFileStorageOptions> options) : IFileStorage
{
    private const string TemporaryPrefix = ".upload-";
    private const int BufferSize = 81920;

    public async Task WriteAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var path = PathOf(key);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $"{TemporaryPrefix}{Guid.NewGuid():N}");
        try
        {
            await using (var target = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous))
            {
                await content.CopyToAsync(target, BufferSize, cancellationToken);
            }

            // Keys are new ids, so an existing file means a bug; never overwrite content.
            File.Move(temporaryPath, path, overwrite: false);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathOf(key);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, BufferSize, FileOptions.Asynchronous));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        // Deleting a missing file is not an error, so retried deletes succeed.
        File.Delete(PathOf(key));
        return Task.CompletedTask;
    }

    // Keys come from the application, but the path is still confined to the root: no key can escape it.
    private string PathOf(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var root = Root();
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(root, StringComparison.Ordinal) || Path.GetFileName(path).StartsWith(TemporaryPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("The storage key does not name a file inside the storage root.", nameof(key));
        }

        return path;
    }

    private string Root()
    {
        var configured = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new ConflictException(
                $"File storage is not configured: set {LocalFileStorageOptions.SectionName}:{nameof(LocalFileStorageOptions.RootPath)}.");
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured)) + Path.DirectorySeparatorChar;
    }
}
