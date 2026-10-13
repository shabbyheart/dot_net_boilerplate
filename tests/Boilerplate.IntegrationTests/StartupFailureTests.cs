using System.Diagnostics;

namespace Boilerplate.IntegrationTests;

/// <summary>A host that fails to start must exit with a non-zero code (ADR-0007).</summary>
public sealed class StartupFailureTests
{
    [Fact]
    public async Task Host_exits_with_code_1_when_configuration_is_invalid()
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

        // Invalid configuration: an unknown log level fails while the host builds its logger.
        startInfo.Environment["Serilog__MinimumLevel__Default"] = "NotALevel";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        startInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";

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
