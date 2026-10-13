using System.Diagnostics;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests;

/// <summary>
/// Invalid configuration stops the real process with exit code 1 and logs the reason (ADR-0007).
/// Runs the API as a child process because the exit code is only observable there.
/// </summary>
public sealed class StartupFailureTests
{
    private const string ValidConnectionString = "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused";

    [Theory]
    [InlineData("Database__ConnectionString", "", "The ConnectionString field is required.")]
    [InlineData("Serilog__MinimumLevel__Default", "NotALevel", "The value NotALevel is not a valid Serilog level.")]
    [InlineData(
        "Authentication__Schemes__Bearer__Authority",
        "",
        "Authentication:Schemes:Bearer needs either Authority, or ValidIssuer together with SigningKeys.")]
    [InlineData("Authentication__Schemes__Bearer__ValidAudiences__0", "", "Authentication:Schemes:Bearer:ValidAudiences is required.")]
    public async Task Host_exits_with_code_1_when_configuration_is_invalid(string setting, string value, string expectedReason)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var apiAssemblyPath = Path.Combine(AppContext.BaseDirectory, "Boilerplate.Api.dll");

        var startInfo = new ProcessStartInfo(DotnetHostPath(), [apiAssemblyPath])
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // A valid baseline, so the setting under test is the only reason for the failure.
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        startInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        startInfo.Environment["Database__ConnectionString"] = ValidConnectionString;
        startInfo.Environment["Authentication__Schemes__Bearer__Authority"] = "https://issuer.example";
        startInfo.Environment["Authentication__Schemes__Bearer__ValidAudiences__0"] = TestTokens.Audience;
        startInfo.Environment[setting] = value;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the API process.");

        try
        {
            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);

            var output = await standardOutput + await standardError;
            process.ExitCode.ShouldBe(1, output);
            output.ShouldContain("Host terminated unexpectedly");
            output.ShouldContain(expectedReason);
        }
        finally
        {
            // A host that unexpectedly starts would keep running and hold the test runner's output pipes open.
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    // The .NET CLI sets DOTNET_HOST_PATH for processes it starts; fall back to dotnet on PATH.
    private static string DotnetHostPath() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } path ? path : "dotnet";
}
