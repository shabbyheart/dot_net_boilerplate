using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// Hosts the real API in memory in the Development environment, connected to the test PostgreSQL container,
/// accepting tokens from <see cref="TestTokens"/>, plus test-only routes that throw
/// (see <see cref="ThrowingRoutesStartupFilter"/>) and test-only controllers (see <see cref="PermissionProbeController"/>).
/// </summary>
public sealed class ApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = postgres.ConnectionString,

                // Test hosts share one database; only tests that exercise the dispatcher turn it on (OutboxDispatchTests).
                ["Outbox:Enabled"] = "false",

                // Deletes rows by age across the shared database; HousekeepingTests turn it on against their own.
                ["Housekeeping:Enabled"] = "false",
            })
            .AddInMemoryCollection(PersistenceServices.UnusedSmtpSettings)
            .AddInMemoryCollection(TestTokens.ApiSettings));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, ThrowingRoutesStartupFilter>();

            // Test-only controllers such as PermissionProbeController.
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);
        });
    }
}
