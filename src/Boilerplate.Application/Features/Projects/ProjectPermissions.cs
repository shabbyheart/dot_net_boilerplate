namespace Boilerplate.Application.Features.Projects;

/// <summary>Permissions of the projects feature (ADR-0017). Granted to users through roles.</summary>
public static class ProjectPermissions
{
    /// <summary>List and view projects.</summary>
    public const string Read = "projects.read";

    /// <summary>Create, change and delete projects.</summary>
    public const string Manage = "projects.manage";
}
