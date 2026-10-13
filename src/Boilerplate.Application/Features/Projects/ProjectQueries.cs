using Boilerplate.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Projects;

/// <summary>Query logic shared by more than one projects handler (AGENTS.md: extract only when reused).</summary>
internal static class ProjectQueries
{
    /// <summary>Whether another project of the current tenant already uses <paramref name="name"/> (exact match).</summary>
    public static Task<bool> NameIsTakenAsync(
        this IQueryable<Project> projects,
        string name,
        Guid excludingProjectId,
        CancellationToken cancellationToken) =>
        projects.AnyAsync(project => project.Name == name && project.Id != excludingProjectId, cancellationToken);
}
