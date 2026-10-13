using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Boilerplate.Infrastructure.Messaging;

/// <summary>Runs <see cref="OutboxProcessor"/> on a timer while the host runs (ADR-0019).</summary>
internal sealed partial class OutboxDispatcher(
    OutboxProcessor processor,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            LogDisabled();
            return;
        }

        using var timer = new PeriodicTimer(options.Value.PollingInterval, timeProvider);
        try
        {
            do
            {
                await DispatchAvailableAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }

    private async Task DispatchAvailableAsync(CancellationToken stoppingToken)
    {
        try
        {
            // A full batch suggests more are waiting; keep going without waiting for the next tick.
            while (await processor.ProcessBatchAsync(stoppingToken) == options.Value.BatchSize)
            {
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // For example, the database is unreachable: try again on the next tick instead of stopping the host.
            LogDispatchFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox dispatching is disabled on this instance")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox dispatch round failed; retrying on the next tick")]
    private partial void LogDispatchFailed(Exception exception);
}
