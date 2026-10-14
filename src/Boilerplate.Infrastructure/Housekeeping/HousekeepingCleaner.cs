using Boilerplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Boilerplate.Infrastructure.Housekeeping;

/// <summary>
/// Deletes rows that are past their retention (ADR-0026): expired refresh and one-time tokens, expired invitations
/// (ADR-0027) and delivered outbox messages. Dead-lettered outbox messages are kept; they need an operator. Each statement deletes at most one batch,
/// so locks stay short, and deleting is idempotent, so several instances may run it at once.
/// </summary>
public sealed partial class HousekeepingCleaner(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<HousekeepingOptions> options,
    ILogger<HousekeepingCleaner> logger)
{
    /// <summary>Runs one cleanup round until nothing past its retention is left.</summary>
    public async Task<HousekeepingResult> RunAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var tokensExpiredBefore = now - settings.ExpiredTokenRetention;
        var outboxProcessedBefore = now - settings.ProcessedOutboxRetention;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // A replaced refresh token is kept until it expires, so presenting it again still revokes its family (ADR-0025).
        var refreshTokens = await DeleteInBatchesAsync(
            () => db.RefreshTokens
                .Where(token => token.ExpiresAt < tokensExpiredBefore)
                .OrderBy(token => token.Id)
                .Take(settings.BatchSize)
                .ExecuteDeleteAsync(cancellationToken),
            settings.BatchSize);

        var oneTimeTokens = await DeleteInBatchesAsync(
            () => db.OneTimeTokens
                .Where(token => token.ExpiresAt < tokensExpiredBefore)
                .OrderBy(token => token.Id)
                .Take(settings.BatchSize)
                .ExecuteDeleteAsync(cancellationToken),
            settings.BatchSize);

        // Invitations are tenant-owned; cleanup covers every tenant, so it bypasses the tenant filter by name.
        var invitations = await DeleteInBatchesAsync(
            () => db.Invitations
                .IgnoreQueryFilters([AppDbContext.TenantQueryFilter])
                .Where(invitation => invitation.ExpiresAt < tokensExpiredBefore)
                .OrderBy(invitation => invitation.Id)
                .Take(settings.BatchSize)
                .ExecuteDeleteAsync(cancellationToken),
            settings.BatchSize);

        var outboxMessages = await DeleteInBatchesAsync(
            () => db.OutboxMessages
                .Where(message => message.ProcessedAt < outboxProcessedBefore)
                .OrderBy(message => message.Id)
                .Take(settings.BatchSize)
                .ExecuteDeleteAsync(cancellationToken),
            settings.BatchSize);

        var result = new HousekeepingResult(refreshTokens, oneTimeTokens, invitations, outboxMessages);
        if (result.Total > 0)
        {
            LogDeleted(refreshTokens, oneTimeTokens, invitations, outboxMessages);
        }

        return result;
    }

    // A full batch suggests more rows are waiting.
    private static async Task<int> DeleteInBatchesAsync(Func<Task<int>> deleteBatchAsync, int batchSize)
    {
        var total = 0;
        int deleted;
        do
        {
            deleted = await deleteBatchAsync();
            total += deleted;
        }
        while (deleted == batchSize);

        return total;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Housekeeping deleted {RefreshTokens} refresh tokens, {OneTimeTokens} one-time tokens, {Invitations} invitations and {OutboxMessages} delivered outbox messages")]
    private partial void LogDeleted(int refreshTokens, int oneTimeTokens, int invitations, int outboxMessages);
}

/// <summary>Rows deleted by one cleanup round.</summary>
public sealed record HousekeepingResult(int RefreshTokens, int OneTimeTokens, int Invitations, int OutboxMessages)
{
    public int Total => RefreshTokens + OneTimeTokens + Invitations + OutboxMessages;
}
