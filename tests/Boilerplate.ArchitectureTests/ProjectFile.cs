using System.Xml.Linq;

namespace Boilerplate.ArchitectureTests;

/// <summary>
/// Reads declared references from MSBuild files. Declared references are checked instead of compiled
/// assembly references because the compiler drops references that no code uses yet.
/// </summary>
internal sealed record ProjectFile(string Name, IReadOnlySet<string> ProjectReferences, IReadOnlySet<string> PackageReferences)
{
    public static ProjectFile Load(string path)
    {
        var document = XDocument.Load(path);

        var projectReferences = IncludeValues(document, "ProjectReference")
            .Select(ProjectNameFromPath)
            .ToHashSet(StringComparer.Ordinal);

        return new ProjectFile(
            Path.GetFileNameWithoutExtension(path),
            projectReferences,
            IncludeValues(document, "PackageReference").ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    public static IEnumerable<string> IncludeValues(XDocument document, string itemName) =>
        document.Descendants()
            .Where(element => element.Name.LocalName == itemName)
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>();

    // Include paths use Windows separators; normalize so the name resolves on Linux CI as well.
    private static string ProjectNameFromPath(string includePath) =>
        Path.GetFileNameWithoutExtension(includePath.Replace('\\', '/'));
}
