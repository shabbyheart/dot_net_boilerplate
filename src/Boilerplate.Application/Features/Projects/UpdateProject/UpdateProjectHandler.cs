using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Projects.UpdateProject;

public sealed class UpdateProjectHandler(IAppDbContext db)
{
    /// <exception cref="NotFoundException">No project with this id exists in the current tenant.</exception>
    /// <exception cref="ConflictException">Another project already uses the new name.</exception>
    public async Task<ProjectResponse> HandleAsync(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project", projectId);

        project.Update(request.Name, request.Description);

        if (await db.Projects.NameIsTakenAsync(project.Name, project.Id, cancellationToken))
        {
            throw new ConflictException($"A project named '{project.Name}' already exists.");
        }

        await db.SaveChangesAsync(cancellationToken);

        return ProjectResponse.From(project);
    }
}
