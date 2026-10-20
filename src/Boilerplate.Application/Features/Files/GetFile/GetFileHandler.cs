using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Files.GetFile;

public sealed class GetFileHandler(IAppDbContext db)
{
    /// <exception cref="NotFoundException">No file with this id exists in the current tenant.</exception>
    public async Task<FileResponse> HandleAsync(Guid fileId, CancellationToken cancellationToken) =>
        await db.Files
            .AsNoTracking()
            .Where(file => file.Id == fileId)
            .Select(FileResponse.FromFile)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("File", fileId);
}
