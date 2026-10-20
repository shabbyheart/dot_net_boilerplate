using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Storage;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Files.DownloadFile;

public sealed class DownloadFileHandler(IAppDbContext db, IFileStorage storage)
{
    /// <exception cref="NotFoundException">No file with this id exists in the current tenant, or its content is missing.</exception>
    /// <exception cref="ConflictException">File storage is not configured.</exception>
    public async Task<FileDownload> HandleAsync(Guid fileId, CancellationToken cancellationToken)
    {
        // The tenant filter decides access; the storage key alone grants nothing.
        var file = await db.Files
            .AsNoTracking()
            .SingleOrDefaultAsync(f => f.Id == fileId, cancellationToken)
            ?? throw new NotFoundException("File", fileId);

        var content = await storage.OpenReadAsync(file.StorageKey, cancellationToken)
            ?? throw new NotFoundException("File content", fileId);

        return new FileDownload(content, file.ContentType, file.FileName, file.Length);
    }
}
