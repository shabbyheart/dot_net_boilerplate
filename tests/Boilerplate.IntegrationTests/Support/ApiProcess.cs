using System.Diagnostics;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// Runs the built API as a real process, for behaviour only observable there: exit codes and one-off commands.
/// </summary>
internal static class ApiProcess
{
    public sealed record Result(int ExitCode, string Output);

    public static async Task<Result> RunAsync(
        IReadOnlyDictionary<string, string> environment,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(DotnetHostPath())
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Boilerplate.Api.dll"));
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the API process.");

        try
        {
            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);

            return new Result(process.ExitCode, await standardOutput + await standardError);
        }
        finally
        {
            // A process that unexpectedly keeps running would hold the test runner's output pipes open.
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
