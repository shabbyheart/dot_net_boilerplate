using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// Hosts the real API in memory in the Development environment, connected to the test PostgreSQL container,
/// accepting tokens from <see cref="TestTokens"/>, plus test-only routes that throw
/// (see <see cref="ThrowingRoutesStartupFilter"/>).
/// </summary>
public sealed class ApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = postgres.ConnectionString })
            .AddInMemoryCollection(TestTokens.ApiSettings));
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ThrowingRoutesStartupFilter>());
    }
}
