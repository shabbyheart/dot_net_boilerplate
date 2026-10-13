using Boilerplate.Application.Common.Security;
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
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<TestCurrentUser>());
        services.AddInfrastructure();
        configure?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>A scope acting for <paramref name="tenantId"/> and <paramref name="userId"/> (either may be none).</summary>
    public static AsyncServiceScope CreateScope(this IServiceProvider services, Guid? tenantId, string? userId = null)
    {
        var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestTenantContext>().TenantId = tenantId;
        scope.ServiceProvider.GetRequiredService<TestCurrentUser>().UserId = userId;
        return scope;
    }
}
