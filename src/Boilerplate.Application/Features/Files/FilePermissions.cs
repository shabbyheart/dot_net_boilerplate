namespace Boilerplate.Application.Features.Files;

/// <summary>Permissions of file storage (ADR-0030).</summary>
public static class FilePermissions
{
    /// <summary>List files and download their content.</summary>
    public const string Read = "files.read";

    /// <summary>Upload and delete files.</summary>
    public const string Manage = "files.manage";
}
