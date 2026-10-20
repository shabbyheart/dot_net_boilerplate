using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Storage;
using Boilerplate.Domain.Files;

namespace Boilerplate.Application.Features.Files.UploadFile;

public sealed class UploadFileHandler(IAppDbContext db, IFileStorage storage)
{
    /// <summary>Stores the content, then its description (ADR-0030).</summary>
    /// <exception cref="ConflictException">File storage is not configured.</exception>
    public async Task<FileResponse> HandleAsync(UploadFileRequest request, CancellationToken cancellationToken)
    {
        var file = StoredFile.Create(request.FileName, request.ContentType, request.Length);

        // Content first: the description must never point at content that does not exist. Content cannot join the
        // database transaction, so a failed save removes it again.
        await storage.WriteAsync(file.StorageKey, request.Content, cancellationToken);
        db.Files.Add(file);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Not the request's token: the cleanup must run even when the request was cancelled.
            await storage.DeleteAsync(file.StorageKey, CancellationToken.None);
            throw;
        }

        return FileResponse.From(file);
    }
}
