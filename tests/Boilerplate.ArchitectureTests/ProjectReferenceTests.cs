namespace Boilerplate.ArchitectureTests;

/// <summary>
/// Enforces the project dependency graph from docs/adr/0001-architecture-style.md.
/// Fix the code, not these tests; changing a rule requires a superseding ADR.
/// </summary>
public sealed class ProjectReferenceTests
{
    private static readonly Dictionary<string, string[]> AllowedReferences = new(StringComparer.Ordinal)
    {
        ["Boilerplate.Domain"] = [],
        ["Boilerplate.Application"] = ["Boilerplate.Domain"],
        ["Boilerplate.Infrastructure"] = ["Boilerplate.Application"],
        ["Boilerplate.Api"] = ["Boilerplate.Application", "Boilerplate.Infrastructure"],
    };

    public static TheoryData<string> SourceProjects => new(AllowedReferences.Keys);

    [Theory]
    [MemberData(nameof(SourceProjects))]
    public void Source_project_references_only_allowed_projects(string projectName)
    {
        var project = ProjectFile.Load(Repository.SourceProjectPath(projectName));

        project.ProjectReferences.Order(StringComparer.Ordinal).ShouldBe(
            AllowedReferences[projectName].Order(StringComparer.Ordinal),
            $"{projectName} has project references outside the allowed graph in docs/architecture.md.");
    }

    [Fact]
    public void Every_source_project_has_a_dependency_rule()
    {
        var sourceProjects = Directory.EnumerateDirectories(Repository.SourceDirectory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal);

        sourceProjects.ShouldBe(
            AllowedReferences.Keys.Order(StringComparer.Ordinal),
            "A new source project needs an ADR and an entry in AllowedReferences.");
    }
}
