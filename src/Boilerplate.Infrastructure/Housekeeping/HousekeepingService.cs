using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Boilerplate.Infrastructure.Housekeeping;

/// <summary>Runs <see cref="HousekeepingCleaner"/> at startup and then on a timer while the host runs (ADR-0026).</summary>
internal sealed partial class HousekeepingService(
    HousekeepingCleaner cleaner,
    TimeProvider timeProvider,
    IOptions<HousekeepingOptions> options,
    ILogger<HousekeepingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            LogDisabled();
            return;
        }

        using var timer = new PeriodicTimer(options.Value.Interval, timeProvider);
        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await cleaner.RunAsync(stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // For example, the database is unreachable or not migrated yet: try again on the next tick.
            LogRunFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Housekeeping is disabled on this instance")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Error, Message = "Housekeeping round failed; retrying on the next tick")]
    private partial void LogRunFailed(Exception exception);
}
