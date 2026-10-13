using Boilerplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Boilerplate.IntegrationTests.Support.PostgresFixture))]

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// One PostgreSQL container for the whole test assembly, with the real migrations applied (ADR-0003, ADR-0009).
/// Tests isolate their data by creating their own tenants, so no reset between tests is needed.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Same image as deploy/compose.yaml.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>A connection string for another database in the same server (created on first use).</summary>
    public string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(ConnectionString) { Database = database }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var services = PersistenceServices.Create(ConnectionString);
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
