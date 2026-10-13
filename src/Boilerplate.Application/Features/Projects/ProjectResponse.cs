using System.Linq.Expressions;
using Boilerplate.Domain.Projects;

namespace Boilerplate.Application.Features.Projects;

/// <summary>A project as returned by the API.</summary>
public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Hand-written mapping (ADR-0015); EF Core translates it to a column projection in queries.</summary>
    public static readonly Expression<Func<Project, ProjectResponse>> FromProject = project => new ProjectResponse(
        project.Id,
        project.Name,
        project.Description,
        project.CreatedAt,
        project.UpdatedAt);

    private static readonly Func<Project, ProjectResponse> MapProject = FromProject.Compile();

    /// <summary>Maps an entity already in memory (after a save), with the same mapping as queries.</summary>
    public static ProjectResponse From(Project project) => MapProject(project);
}
