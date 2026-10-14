using Boilerplate.Infrastructure.Persistence;
using Boilerplate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Housekeeping;

/// <summary>
/// A separate, migrated database for housekeeping tests: cleanup deletes rows by age, which must never touch the
/// rows of tests running at the same time.
/// </summary>
public sealed class HousekeepingDatabase(PostgresFixture postgres) : IAsyncLifetime
{
    public string ConnectionString { get; } = postgres.ConnectionStringFor("housekeeping_tests");

    public async ValueTask InitializeAsync()
    {
        await using var services = PersistenceServices.Create(ConnectionString);
        await using var scope = services.CreateScope(tenantId: null);
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
