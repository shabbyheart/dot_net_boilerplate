using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// Builds a service provider through the real <see cref="DependencyInjection.AddInfrastructure"/>, so persistence
/// tests exercise the production wiring (provider, conventions, interceptors) with a controllable tenant and clock.
/// </summary>
internal static class PersistenceServices
{
    public static ServiceProvider Create(
        string connectionString,
        TimeProvider? timeProvider = null,
        Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = connectionString })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        if (timeProvider is not null)
        {
            services.AddSingleton(timeProvider);
        }

        services.AddScoped<TestTenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TestTenantContext>());
        services.AddInfrastructure();
        configure?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>A scope acting for <paramref name="tenantId"/> (or for no tenant).</summary>
    public static AsyncServiceScope CreateScope(this IServiceProvider services, Guid? tenantId)
    {
        var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestTenantContext>().TenantId = tenantId;
        return scope;
    }
}
