using Boilerplate.Domain.Accounts;
using Boilerplate.Infrastructure.Housekeeping;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Boilerplate.IntegrationTests.Housekeeping;

/// <summary>Retention cleanup (ADR-0026) with a controlled clock, and the hosted service in a running API.</summary>
public sealed class HousekeepingTests(HousekeepingDatabase database, ApiFactory factory)
    : IClassFixture<HousekeepingDatabase>, IClassFixture<ApiFactory>
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    private DateTimeOffset Now => _clock.GetUtcNow();

    [Fact]
    public async Task Cleanup_deletes_only_rows_past_their_retention()
    {
        await using var services = await CreateServicesAsync();

        var expiredLongAgo = RefreshToken.Issue("user-1", tenantId: null, NewHash(), Now.AddDays(-30), TimeSpan.FromDays(14));
        var recentlyExpired = RefreshToken.Issue("user-1", tenantId: null, NewHash(), Now.AddDays(-20), TimeSpan.FromDays(14));

        // Replaced but not expired: kept, so presenting it again still revokes the family.
        var replaced = RefreshToken.Issue("user-1", tenantId: null, NewHash(), Now.AddDays(-1), TimeSpan.FromDays(14));
        var current = replaced.Rotate(NewHash(), Now.AddHours(-1), TimeSpan.FromDays(14));

        var oldLink = OneTimeToken.Issue("user-1", OneTimeTokenPurpose.PasswordReset, NewHash(), Now.AddDays(-10), TimeSpan.FromHours(1));
        var recentLink = OneTimeToken.Issue("user-1", OneTimeTokenPurpose.EmailConfirmation, NewHash(), Now.AddDays(-3), TimeSpan.FromDays(1));
        var usedLink = OneTimeToken.Issue("user-1", OneTimeTokenPurpose.PasswordReset, NewHash(), Now.AddMinutes(-5), TimeSpan.FromHours(1));
        usedLink.Redeem(Now);

        await AddAsync(services, [expiredLongAgo, recentlyExpired, replaced, current], [oldLink, recentLink, usedLink]);

        await InsertOutboxRowAsync(services, processedAt: Now.AddDays(-8));
        var deliveredRecently = await InsertOutboxRowAsync(services, processedAt: Now.AddDays(-1));
        var pending = await InsertOutboxRowAsync(services);
        var deadLettered = await InsertOutboxRowAsync(services, deadLetteredAt: Now.AddDays(-30));

        var result = await services.GetRequiredService<HousekeepingCleaner>().RunAsync(_cancellationToken);

        result.ShouldBe(new HousekeepingResult(RefreshTokens: 1, OneTimeTokens: 1, OutboxMessages: 1));

        await using var scope = services.CreateScope(tenantId: null);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.RefreshTokens.Select(token => token.Id).ToListAsync(_cancellationToken))
            .ShouldBe([recentlyExpired.Id, replaced.Id, current.Id], ignoreOrder: true);
        (await db.OneTimeTokens.Select(token => token.Id).ToListAsync(_cancellationToken))
            .ShouldBe([recentLink.Id, usedLink.Id], ignoreOrder: true);
        (await db.OutboxMessages.Select(message => message.Id).ToListAsync(_cancellationToken))
            .ShouldBe([deliveredRecently, pending, deadLettered], ignoreOrder: true);
    }

    [Fact]
    public async Task Cleanup_keeps_deleting_batches_until_nothing_is_left()
    {
        await using var services = await CreateServicesAsync(batchSize: 2);
        var expired = Enumerable.Range(0, 5)
            .Select(_ => RefreshToken.Issue("user-1", tenantId: null, NewHash(), Now.AddDays(-30), TimeSpan.FromDays(1)))
            .ToArray();
        await AddAsync(services, expired, []);

        var result = await services.GetRequiredService<HousekeepingCleaner>().RunAsync(_cancellationToken);

        result.RefreshTokens.ShouldBe(5);
        await using var scope = services.CreateScope(tenantId: null);
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens.CountAsync(_cancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task Running_api_cleans_up_at_startup()
    {
        await using var services = await CreateServicesAsync();
        var delivered = await InsertOutboxRowAsync(services, processedAt: Now.AddDays(-30));

        await using var api = StartApi(enabled: true);

        HousekeepingTask(api).IsCompleted.ShouldBeFalse("the service keeps running between rounds");
        await WaitUntilDeletedAsync(services, delivered);
    }

    [Fact]
    public async Task Disabled_instance_does_not_clean_up()
    {
        await using var services = await CreateServicesAsync();
        var delivered = await InsertOutboxRowAsync(services, processedAt: Now.AddDays(-30));

        await using var api = StartApi(enabled: false);

        // The service returns before touching the database.
        HousekeepingTask(api).IsCompletedSuccessfully.ShouldBeTrue();
        await using var scope = services.CreateScope(tenantId: null);
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages
            .AnyAsync(message => message.Id == delivered, _cancellationToken)).ShouldBeTrue();
    }

    private async Task<ServiceProvider> CreateServicesAsync(int batchSize = 1000)
    {
        var services = PersistenceServices.Create(
            database.ConnectionString,
            _clock,
            settings: new Dictionary<string, string?>
            {
                ["Housekeeping:BatchSize"] = batchSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Housekeeping:ExpiredTokenRetention"] = Retention.ToString(),
                ["Housekeeping:ProcessedOutboxRetention"] = Retention.ToString(),
            });

        await using var scope = services.CreateScope(tenantId: null);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.RefreshTokens.ExecuteDeleteAsync(_cancellationToken);
        await db.OneTimeTokens.ExecuteDeleteAsync(_cancellationToken);
        await db.OutboxMessages.ExecuteDeleteAsync(_cancellationToken);

        return services;
    }

    private WebApplicationFactory<Program> StartApi(bool enabled)
    {
        var api = factory.WithWebHostBuilder(builder => builder
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = database.ConnectionString,
                ["Housekeeping:Enabled"] = enabled ? "true" : "false",
            }))
            .ConfigureServices(services => services.AddSingleton<TimeProvider>(_clock)));

        // Accessing the services starts the host, and with it the hosted services.
        _ = api.Services;
        return api;
    }

    private static Task HousekeepingTask(WebApplicationFactory<Program> api) =>
        api.Services.GetServices<IHostedService>()
            .OfType<BackgroundService>()
            .Single(service => service.GetType().Name == "HousekeepingService")
            .ExecuteTask!;

    private async Task WaitUntilDeletedAsync(IServiceProvider services, Guid outboxMessageId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var scope = services.CreateScope(tenantId: null);
            var exists = await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages
                .AnyAsync(message => message.Id == outboxMessageId, _cancellationToken);
            if (!exists)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), _cancellationToken);
        }

        throw new ShouldAssertException($"Outbox message {outboxMessageId} was not deleted within 10 seconds.");
    }

    private async Task AddAsync(IServiceProvider services, RefreshToken[] refreshTokens, OneTimeToken[] oneTimeTokens)
    {
        await using var scope = services.CreateScope(tenantId: null);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.RefreshTokens.AddRange(refreshTokens);
        db.OneTimeTokens.AddRange(oneTimeTokens);
        await db.SaveChangesAsync(_cancellationToken);
    }

    private async Task<Guid> InsertOutboxRowAsync(
        IServiceProvider services,
        DateTimeOffset? processedAt = null,
        DateTimeOffset? deadLetteredAt = null)
    {
        var id = Guid.CreateVersion7();
        var createdAt = (processedAt ?? deadLetteredAt ?? Now).AddMinutes(-1);
        await using var scope = services.CreateScope(tenantId: null);
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"""
            INSERT INTO outbox_messages (id, type, payload, tenant_id, created_at, available_at, attempts, processed_at, dead_lettered_at)
            VALUES ({id}, {"test.probe"}, {""}, NULL, {createdAt}, {createdAt}, 1, {processedAt}, {deadLetteredAt})
            """,
            _cancellationToken);
        return id;
    }

    private static string NewHash() => Guid.NewGuid().ToString("N");
}
