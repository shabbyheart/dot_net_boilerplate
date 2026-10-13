using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// The built API running as a real process on Kestrel, for behaviour the in-memory test server does not have
/// (for example Kestrel's request body limit). Stopped on dispose.
/// </summary>
internal sealed class ApiServerProcess : IAsyncDisposable
{
    private readonly Process _process;

    private ApiServerProcess(Process process, Uri baseAddress)
    {
        _process = process;
        BaseAddress = baseAddress;
    }

    public Uri BaseAddress { get; }

    public static async Task<ApiServerProcess> StartAsync(
        IReadOnlyDictionary<string, string?> settings,
        CancellationToken cancellationToken)
    {
        var baseAddress = new Uri($"http://127.0.0.1:{FreePort()}");
        var startInfo = new ProcessStartInfo(DotnetHostPath())
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Boilerplate.Api.dll"));
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        startInfo.Environment["ASPNETCORE_URLS"] = baseAddress.ToString();
        foreach (var (key, value) in settings)
        {
            // Configuration keys map to environment variables with "__" as the section separator.
            startInfo.Environment[key.Replace(":", "__", StringComparison.Ordinal)] = value;
        }

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the API process.");

        // Drain output so the process never blocks on a full pipe.
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var server = new ApiServerProcess(process, baseAddress);
        try
        {
            await server.WaitUntilLiveAsync(cancellationToken);
            return server;
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        _process.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task WaitUntilLiveAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = BaseAddress };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException($"The API process exited with code {_process.ExitCode} during startup.");
            }

            try
            {
                using var response = await client.GetAsync("/health/live", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        throw new TimeoutException("The API process did not become live within 60 seconds.");
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string DotnetHostPath() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } path ? path : "dotnet";
}
