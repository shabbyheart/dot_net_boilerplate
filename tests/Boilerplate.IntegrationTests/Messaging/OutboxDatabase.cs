using Boilerplate.Infrastructure.Persistence;
using Boilerplate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Messaging;

/// <summary>
/// A separate, migrated database for outbox processor tests, so their processing never meets messages of other
/// tests (and no running API host's dispatcher touches theirs).
/// </summary>
public sealed class OutboxDatabase(PostgresFixture postgres) : IAsyncLifetime
{
    public string ConnectionString { get; } = postgres.ConnectionStringFor("outbox_tests");

    public async ValueTask InitializeAsync()
    {
        await using var services = PersistenceServices.Create(ConnectionString);
        await using var scope = services.CreateScope(tenantId: null);
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
