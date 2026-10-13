using Boilerplate.Application.Common.Messaging;
using Boilerplate.Infrastructure.Messaging;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Boilerplate.IntegrationTests.Messaging;

/// <summary>Outbox enqueueing, claiming, retries, dead-lettering and leases (ADR-0019), with a controlled clock.</summary>
public sealed class OutboxProcessorTests(OutboxDatabase database) : IClassFixture<OutboxDatabase>
{
    private const int MaxAttempts = 3;

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
    private readonly ProbeRecorder _recorder = new();

    [Fact]
    public async Task Enqueued_message_is_saved_with_the_use_case_and_delivered_for_its_tenant()
    {
        await using var services = await CreateServicesAsync();
        var tenantId = Guid.NewGuid();
        await EnqueueAsync(services, tenantId, "delivered");

        var claimed = await Processor(services).ProcessBatchAsync(_cancellationToken);

        claimed.ShouldBe(1);
        _recorder.Deliveries.ShouldBe([("delivered", (Guid?)tenantId, 1)]);
        var row = await SingleRowAsync(services);
        row.ProcessedAt.ShouldBe(_clock.GetUtcNow());
        row.Payload.ShouldBeEmpty();
        row.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task Enqueue_without_saving_records_nothing()
    {
        await using var services = await CreateServicesAsync();

        await using (var scope = services.CreateScope(Guid.NewGuid()))
        {
            scope.ServiceProvider.GetRequiredService<Outbox>().Enqueue(new ProbeMessage("never-saved"));
        }

        (await CountRowsAsync(services)).ShouldBe(0);
    }

    [Fact]
    public async Task Failed_message_is_retried_after_the_backoff()
    {
        await using var services = await CreateServicesAsync();
        _recorder.Failures["flaky"] = 1;
        await EnqueueAsync(services, tenantId: null, "flaky");

        await Processor(services).ProcessBatchAsync(_cancellationToken);

        var failed = await SingleRowAsync(services);
        failed.Attempts.ShouldBe(1);
        failed.ProcessedAt.ShouldBeNull();
        failed.LastError!.ShouldContain("Probe failure for flaky");
        failed.AvailableAt.ShouldBe(_clock.GetUtcNow() + OutboxProcessor.RetryDelay(1));

        (await Processor(services).ProcessBatchAsync(_cancellationToken)).ShouldBe(0);

        _clock.Advance(OutboxProcessor.RetryDelay(1));
        await Processor(services).ProcessBatchAsync(_cancellationToken);

        _recorder.Deliveries.ShouldBe([("flaky", (Guid?)null, 2)]);
        (await SingleRowAsync(services)).ProcessedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Message_is_dead_lettered_after_the_maximum_attempts()
    {
        await using var services = await CreateServicesAsync();
        _recorder.Failures["broken"] = int.MaxValue;
        await EnqueueAsync(services, tenantId: null, "broken");

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            (await Processor(services).ProcessBatchAsync(_cancellationToken)).ShouldBe(1);
            _clock.Advance(OutboxProcessor.RetryDelay(attempt));
        }

        var row = await SingleRowAsync(services);
        row.DeadLetteredAt.ShouldNotBeNull();
        row.Attempts.ShouldBe(MaxAttempts);
        row.Payload.ShouldNotBeEmpty();

        _clock.Advance(TimeSpan.FromDays(1));
        (await Processor(services).ProcessBatchAsync(_cancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task Unknown_message_type_is_dead_lettered()
    {
        await using var services = await CreateServicesAsync();
        await InsertRowAsync(services, type: "no.such.type", attempts: 0, availableAt: _clock.GetUtcNow());

        await Processor(services).ProcessBatchAsync(_cancellationToken);

        var row = await SingleRowAsync(services);
        row.DeadLetteredAt.ShouldNotBeNull();
        row.LastError.ShouldBe("Unknown message type 'no.such.type'.");
    }

    [Fact]
    public async Task Message_whose_handler_crashed_is_retried_after_the_lease()
    {
        await using var services = await CreateServicesAsync();

        // As if another instance claimed it and died: one attempt counted, lease running.
        var lease = TimeSpan.FromMinutes(5);
        await InsertRowAsync(services, ProbeMessage.OutboxType, attempts: 1, availableAt: _clock.GetUtcNow() + lease);

        (await Processor(services).ProcessBatchAsync(_cancellationToken)).ShouldBe(0);

        _clock.Advance(lease);
        (await Processor(services).ProcessBatchAsync(_cancellationToken)).ShouldBe(1);

        _recorder.Deliveries.ShouldBe([("crashed", (Guid?)null, 2)]);
    }

    [Fact]
    public async Task Concurrent_processors_deliver_every_message_exactly_once()
    {
        // Two independent service providers stand in for two API instances.
        await using var instanceA = await CreateServicesAsync();
        await using var instanceB = await CreateServicesAsync(clearTable: false);
        var keys = Enumerable.Range(1, 30).Select(i => $"message-{i}").ToArray();
        foreach (var key in keys)
        {
            await EnqueueAsync(instanceA, tenantId: null, key);
        }

        await Task.WhenAll(DrainAsync(Processor(instanceA)), DrainAsync(Processor(instanceB)));

        _recorder.Deliveries.Select(delivery => delivery.Key).Order(StringComparer.Ordinal)
            .ShouldBe(keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(10, 3600)]
    [InlineData(50, 3600)]
    public void Retry_delay_doubles_from_10_seconds_up_to_one_hour(int failedAttempts, int expectedSeconds) =>
        OutboxProcessor.RetryDelay(failedAttempts).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));

    private async Task<ServiceProvider> CreateServicesAsync(bool clearTable = true)
    {
        var services = PersistenceServices.Create(
            database.ConnectionString,
            _clock,
            configure: s =>
            {
                s.AddSingleton(_recorder);
                s.AddOutboxMessage<ProbeMessage, ProbeHandler>(ProbeMessage.OutboxType);
            },
            settings: new Dictionary<string, string?>
            {
                ["Outbox:MaxAttempts"] = MaxAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Outbox:BatchSize"] = "10",
            });

        if (clearTable)
        {
            await using var scope = services.CreateScope(tenantId: null);
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages.ExecuteDeleteAsync(_cancellationToken);
        }

        return services;
    }

    private static OutboxProcessor Processor(IServiceProvider services) => services.GetRequiredService<OutboxProcessor>();

    private async Task DrainAsync(OutboxProcessor processor)
    {
        while (await processor.ProcessBatchAsync(_cancellationToken) > 0)
        {
        }
    }

    private async Task EnqueueAsync(IServiceProvider services, Guid? tenantId, string key)
    {
        await using var scope = services.CreateScope(tenantId);
        scope.ServiceProvider.GetRequiredService<Outbox>().Enqueue(new ProbeMessage(key));
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync(_cancellationToken);
    }

    private async Task InsertRowAsync(IServiceProvider services, string type, int attempts, DateTimeOffset availableAt)
    {
        await using var scope = services.CreateScope(tenantId: null);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var createdAt = _clock.GetUtcNow();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO outbox_messages (id, type, payload, tenant_id, created_at, available_at, attempts)
            VALUES ({Guid.CreateVersion7()}, {type}, {"{\"key\":\"crashed\"}"}, NULL, {createdAt}, {availableAt}, {attempts})
            """,
            _cancellationToken);
    }

    private async Task<OutboxMessage> SingleRowAsync(IServiceProvider services)
    {
        await using var scope = services.CreateScope(tenantId: null);
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages
            .AsNoTracking()
            .SingleAsync(_cancellationToken);
    }

    private async Task<int> CountRowsAsync(IServiceProvider services)
    {
        await using var scope = services.CreateScope(tenantId: null);
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages.CountAsync(_cancellationToken);
    }
}
