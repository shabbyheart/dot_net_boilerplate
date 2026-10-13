using Boilerplate.Application.Common.Messaging;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Boilerplate.Infrastructure.Messaging;

/// <summary>
/// Claims and delivers one batch of outbox messages (ADR-0019). Claiming is one short statement; no database
/// transaction stays open while handlers call external services (ADR-0011).
/// </summary>
public sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    OutboxMessageTypes messageTypes,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger)
{
    private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromHours(1);

    /// <summary>Delay before the next attempt after <paramref name="failedAttempts"/> failures: 10 s, doubling, at most 1 h.</summary>
    public static TimeSpan RetryDelay(int failedAttempts)
    {
        var exponent = Math.Clamp(failedAttempts - 1, 0, 20);
        var delay = FirstRetryDelay * Math.Pow(2, exponent);
        return delay < MaxRetryDelay ? delay : MaxRetryDelay;
    }

    /// <summary>Claims up to the batch size of due messages and handles each one. Returns how many were claimed.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var claimed = await ClaimAsync(cancellationToken);
        foreach (var message in claimed)
        {
            await ProcessAsync(message, cancellationToken);
        }

        return claimed.Count;
    }

    // SKIP LOCKED lets several instances claim concurrently without taking the same rows. The claim moves
    // available_at forward by the lease, so a message whose handler crashed becomes due again afterwards.
    private async Task<List<OutboxMessage>> ClaimAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var leaseUntil = now + options.Value.Lease;
        var batchSize = options.Value.BatchSize;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.OutboxMessages
            .FromSql($"""
                UPDATE outbox_messages
                SET available_at = {leaseUntil}, attempts = attempts + 1
                WHERE id IN (
                    SELECT id FROM outbox_messages
                    WHERE processed_at IS NULL AND dead_lettered_at IS NULL AND available_at <= {now}
                    ORDER BY available_at, id
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private async Task ProcessAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var registration = messageTypes.Find(message.Type);
        if (registration is null)
        {
            await DeadLetterAsync(message, $"Unknown message type '{message.Type}'.", cancellationToken);
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            if (message.TenantId is { } tenantId)
            {
                scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(tenantId);
            }

            var context = new OutboxMessageContext(message.Id, message.TenantId, message.Attempts);
            await registration.HandleAsync(scope.ServiceProvider, message.Payload, context, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            await FailAsync(message, exception, cancellationToken);
            return;
        }

        await MarkProcessedAsync(message, cancellationToken);
    }

    private async Task MarkProcessedAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // The payload is cleared: delivered content (links, codes) must not be retained (ADR-0019).
        await UpdateAsync(
            message,
            setters => setters
                .SetProperty(m => m.ProcessedAt, now)
                .SetProperty(m => m.Payload, string.Empty)
                .SetProperty(m => m.LastError, (string?)null),
            cancellationToken);

        LogProcessed(message.Id, message.Type);
    }

    private async Task FailAsync(OutboxMessage message, Exception exception, CancellationToken cancellationToken)
    {
        var error = Truncate($"{exception.GetType().Name}: {exception.Message}");

        if (message.Attempts >= options.Value.MaxAttempts)
        {
            await DeadLetterAsync(message, error, cancellationToken);
            return;
        }

        var retryAt = timeProvider.GetUtcNow() + RetryDelay(message.Attempts);
        await UpdateAsync(
            message,
            setters => setters
                .SetProperty(m => m.AvailableAt, retryAt)
                .SetProperty(m => m.LastError, error),
            cancellationToken);

        LogRetryScheduled(exception, message.Id, message.Type, message.Attempts, retryAt);
    }

    private async Task DeadLetterAsync(OutboxMessage message, string reason, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var error = Truncate(reason);
        await UpdateAsync(
            message,
            setters => setters
                .SetProperty(m => m.DeadLetteredAt, now)
                .SetProperty(m => m.LastError, error),
            cancellationToken);

        LogDeadLettered(message.Id, message.Type, message.Attempts, error);
    }

    private async Task UpdateAsync(
        OutboxMessage message,
        Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<OutboxMessage>> setters,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.OutboxMessages.Where(m => m.Id == message.Id).ExecuteUpdateAsync(setters, cancellationToken);
    }

    private static string Truncate(string value) =>
        value.Length <= OutboxMessage.LastErrorMaxLength ? value : value[..OutboxMessage.LastErrorMaxLength];

    [LoggerMessage(Level = LogLevel.Debug, Message = "Outbox message {MessageId} ({MessageType}) delivered")]
    private partial void LogProcessed(Guid messageId, string messageType);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Outbox message {MessageId} ({MessageType}) failed on attempt {Attempt}; retrying at {RetryAt}")]
    private partial void LogRetryScheduled(Exception exception, Guid messageId, string messageType, int attempt, DateTimeOffset retryAt);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Outbox message {MessageId} ({MessageType}) dead-lettered after {Attempts} attempts: {Reason}")]
    private partial void LogDeadLettered(Guid messageId, string messageType, int attempts, string reason);
}
