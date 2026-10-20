using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Files.ListFiles;

public sealed class ListFilesHandler(IAppDbContext db)
{
    public Task<PagedResponse<FileResponse>> HandleAsync(ListFilesRequest request, CancellationToken cancellationToken) =>
        db.Files
            .AsNoTracking()
            .OrderByDescending(file => file.CreatedAt)
            .ThenByDescending(file => file.Id)
            .Select(FileResponse.FromFile)
            .ToPagedResponseAsync(request, cancellationToken);
}
