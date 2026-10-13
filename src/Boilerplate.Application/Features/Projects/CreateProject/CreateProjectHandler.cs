using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Domain.Projects;

namespace Boilerplate.Application.Features.Projects.CreateProject;

public sealed class CreateProjectHandler(IAppDbContext db)
{
    public async Task<ProjectResponse> HandleAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        // The entity normalizes the name, so check uniqueness against the normalized value.
        var project = Project.Create(request.Name, request.Description);

        if (await db.Projects.NameIsTakenAsync(project.Name, project.Id, cancellationToken))
        {
            throw new ConflictException($"A project named '{project.Name}' already exists.");
        }

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        return ProjectResponse.From(project);
    }
}
