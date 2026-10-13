using System.Xml.Linq;

namespace Boilerplate.ArchitectureTests;

/// <summary>Enforces the package policy from docs/adr/0002-platform-and-package-policy.md.</summary>
public sealed class PackagePolicyTests
{
    // Packages excluded by accepted decisions. Prefix match, case-insensitive.
    private static readonly string[] BannedPackagePrefixes =
    [
        "MediatR", // ADR-0004: plain handler classes, no mediator library.
        "Ardalis.", // ADR-0002: no Specification pattern or generic repository.
    ];

    [Fact]
    public void Domain_has_no_package_references()
    {
        var domain = ProjectFile.Load(Repository.SourceProjectPath("Boilerplate.Domain"));

        domain.PackageReferences.ShouldBeEmpty(
            "Domain is dependency-free. Adding a package needs an ADR (ADR-0001).");
    }

    [Fact]
    public void Application_references_only_allowlisted_packages()
    {
        var application = ProjectFile.Load(Repository.SourceProjectPath("Boilerplate.Application"));

        var notAllowed = application.PackageReferences.Where(package => !IsAllowedInApplication(package));

        notAllowed.ShouldBeEmpty(
            "Application packages are allowlisted. Extend IsAllowedInApplication only together with an accepted ADR.");
    }

    [Fact]
    public void No_project_uses_a_banned_package()
    {
        var used = Repository.MsBuildFiles()
            .SelectMany(path =>
            {
                var document = XDocument.Load(path);
                return ProjectFile.IncludeValues(document, "PackageReference")
                    .Concat(ProjectFile.IncludeValues(document, "PackageVersion"));
            });

        var banned = used
            .Where(package => BannedPackagePrefixes.Any(prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        banned.ShouldBeEmpty("These packages are excluded by accepted ADRs.");
    }

    // Each allowlist entry must point to the ADR that approved it.
    private static bool IsAllowedInApplication(string package)
    {
        // ADR-0001: abstractions-only Microsoft.Extensions packages (options, logging, DI abstractions).
        var isExtensionsAbstractions = package.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase)
            && package.EndsWith(".Abstractions", StringComparison.OrdinalIgnoreCase);

        // ADR-0005: EF Core itself for DbSet<T>; never a provider or the relational package.
        var isEntityFrameworkCore = package.Equals("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase);

        // ADR-0014: FluentValidation itself; not its ASP.NET Core or assembly-scanning packages.
        var isFluentValidation = package.Equals("FluentValidation", StringComparison.OrdinalIgnoreCase);

        return isExtensionsAbstractions || isEntityFrameworkCore || isFluentValidation;
    }
}
