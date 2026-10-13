using NetArchTest.Rules;
using ArchTestResult = NetArchTest.Rules.TestResult;

namespace Boilerplate.ArchitectureTests;

/// <summary>
/// Enforces type-level dependency rules from docs/architecture.md on compiled code.
/// These complement <see cref="ProjectReferenceTests"/>: a declared reference is caught there,
/// actual use of a forbidden namespace is caught here.
/// Assemblies are read as files (metadata only) rather than loaded into the test process.
/// </summary>
public sealed class LayerDependencyTests
{
    private static Types Domain => Layer("Boilerplate.Domain");

    private static Types Application => Layer("Boilerplate.Application");

    private static Types Infrastructure => Layer("Boilerplate.Infrastructure");

    private static Types Api => Layer("Boilerplate.Api");

    [Fact]
    public void Domain_does_not_depend_on_other_layers_or_frameworks()
    {
        var result = Domain
            .ShouldNot()
            .HaveDependencyOnAny(
                "Boilerplate.Application",
                "Boilerplate.Infrastructure",
                "Boilerplate.Api",
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_api_aspnetcore_or_database_provider()
    {
        var result = Application
            .ShouldNot()
            .HaveDependencyOnAny("Boilerplate.Infrastructure", "Boilerplate.Api", "Microsoft.AspNetCore", "Npgsql")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        var result = Infrastructure
            .ShouldNot()
            .HaveDependencyOn("Boilerplate.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Api_uses_infrastructure_only_for_composition()
    {
        // Program.cs (global namespace) and Boilerplate.Api.Registration may register Infrastructure services.
        // Endpoints and everything else go through Application.
        var result = Api
            .That()
            .ResideInNamespaceStartingWith("Boilerplate.Api")
            .And()
            .DoNotResideInNamespaceStartingWith("Boilerplate.Api.Registration")
            .ShouldNot()
            .HaveDependencyOn("Boilerplate.Infrastructure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    // The project references copy each layer assembly next to the test assembly.
    private static Types Layer(string assemblyName) =>
        Types.FromFile(Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll"));

    private static string Describe(ArchTestResult result) =>
        "Types violating the rule: " + string.Join(", ", result.FailingTypeNames ?? []);
}
