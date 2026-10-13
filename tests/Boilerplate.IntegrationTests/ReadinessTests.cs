using System.Diagnostics;
using System.Net;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Boilerplate.IntegrationTests;

/// <summary>Readiness reflects whether the database is reachable and migrated (ADR-0011, ADR-0024).</summary>
public sealed class ReadinessTests(ApiFactory factory, PostgresFixture postgres) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Ready_returns_503_until_migrations_are_applied()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = $"unmigrated_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(postgres.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        var connectionString = postgres.ConnectionStringFor(database);
        await using var api = WithConnectionString(connectionString);
        using var client = api.CreateClient();

        using (var beforeMigration = await client.GetAsync("/health/ready", cancellationToken))
        {
            beforeMigration.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        }

        await using (var services = PersistenceServices.Create(connectionString))
        await using (var scope = services.CreateScope(tenantId: null))
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);
        }

        using var afterMigration = await client.GetAsync("/health/ready", cancellationToken);
        afterMigration.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_returns_200_when_the_database_is_reachable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_returns_503_promptly_when_the_database_is_unreachable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Nothing listens on port 1, so every connection attempt is refused and retried.
        await using var unreachable = WithConnectionString(
            "Host=127.0.0.1;Port=1;Database=unreachable;Username=unused;Password=unused;Timeout=3");
        using var client = unreachable.CreateClient();

        var stopwatch = Stopwatch.StartNew();
        using var response = await client.GetAsync("/health/ready", cancellationToken);
        stopwatch.Stop();

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        // Without the health check timeout, connection retries took about a minute.
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15));
    }

    // Later configuration sources win, so this overrides the factory's connection string.
    private WebApplicationFactory<Program> WithConnectionString(string connectionString) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Database:ConnectionString"] = connectionString })));
}
