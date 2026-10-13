using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// Hosts the real API in memory in the Development environment,
/// plus test-only routes that throw (see <see cref="ThrowingRoutesStartupFilter"/>).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ThrowingRoutesStartupFilter>());
    }
}
