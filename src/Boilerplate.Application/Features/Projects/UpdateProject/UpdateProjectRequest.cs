namespace Boilerplate.Application.Features.Projects.UpdateProject;

/// <summary>Replaces the project's editable fields.</summary>
/// <param name="Name">Required; unique within the tenant; at most 200 characters.</param>
/// <param name="Description">Optional; at most 2000 characters. Empty clears it.</param>
public sealed record UpdateProjectRequest(string Name, string? Description);
