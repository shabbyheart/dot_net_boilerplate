using Boilerplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// A migrated database of its own, for tests whose background work acts on every row of a table (outbox dispatch,
/// housekeeping). In the shared database that work would consume or delete rows of tests running at the same time.
/// </summary>
public abstract class SeparateDatabase(PostgresFixture postgres, string name) : IAsyncLifetime
{
    public string ConnectionString { get; } = postgres.ConnectionStringFor(name);

    public async ValueTask InitializeAsync()
    {
        await using var services = PersistenceServices.Create(ConnectionString);
        await using var scope = services.CreateScope(tenantId: null);
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    // The database stays until the container is removed; there is nothing to release.
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
