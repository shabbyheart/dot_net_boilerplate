namespace Boilerplate.ArchitectureTests;

/// <summary>Locates repository files relative to the test output directory.</summary>
internal static class Repository
{
    private const string SolutionFileName = "Boilerplate.slnx";

    public static DirectoryInfo Root { get; } = FindRoot();

    public static string SourceDirectory => Path.Combine(Root.FullName, "src");

    public static string SourceProjectPath(string projectName) =>
        Path.Combine(SourceDirectory, projectName, projectName + ".csproj");

    /// <summary>All MSBuild files that can declare packages, excluding build output.</summary>
    public static IEnumerable<string> MsBuildFiles() =>
        Directory.EnumerateFiles(Root.FullName, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".props", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase))
            .Where(path => !IsBuildOutput(path));

    private static bool IsBuildOutput(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin") || segments.Contains("obj");
    }

    private static DirectoryInfo FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory
            ?? throw new InvalidOperationException($"Could not find {SolutionFileName} above {AppContext.BaseDirectory}.");
    }
}
