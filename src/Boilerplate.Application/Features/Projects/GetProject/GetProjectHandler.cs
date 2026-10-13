using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Projects.GetProject;

public sealed class GetProjectHandler(IAppDbContext db)
{
    /// <exception cref="NotFoundException">No project with this id exists in the current tenant.</exception>
    public async Task<ProjectResponse> HandleAsync(Guid projectId, CancellationToken cancellationToken) =>
        await db.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(ProjectResponse.FromProject)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Project", projectId);
}
