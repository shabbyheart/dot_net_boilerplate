using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Projects.ListProjects;

public sealed class ListProjectsHandler(IAppDbContext db)
{
    public Task<PagedResponse<ProjectResponse>> HandleAsync(ListProjectsRequest request, CancellationToken cancellationToken) =>
        db.Projects
            .AsNoTracking()
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Id)
            .Select(ProjectResponse.FromProject)
            .ToPagedResponseAsync(request, cancellationToken);
}
