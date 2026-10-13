using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Projects.DeleteProject;

public sealed class DeleteProjectHandler(IAppDbContext db)
{
    /// <exception cref="NotFoundException">No project with this id exists in the current tenant.</exception>
    public async Task HandleAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project", projectId);

        db.Projects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);
    }
}
