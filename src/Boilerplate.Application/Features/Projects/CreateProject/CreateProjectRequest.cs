namespace Boilerplate.Application.Features.Projects.CreateProject;

/// <param name="Name">Required; unique within the tenant; at most 200 characters.</param>
/// <param name="Description">Optional; at most 2000 characters.</param>
public sealed record CreateProjectRequest(string Name, string? Description);
