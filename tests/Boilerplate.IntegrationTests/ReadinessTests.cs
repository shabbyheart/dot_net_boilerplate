using System.Diagnostics;
using System.Net;
using Boilerplate.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Boilerplate.IntegrationTests;

/// <summary>Readiness reflects whether the database is reachable (ADR-0011).</summary>
public sealed class ReadinessTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
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
