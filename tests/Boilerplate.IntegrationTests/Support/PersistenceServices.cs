using Boilerplate.Application;
using Boilerplate.Application.Common.Security;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// Builds a service provider through the real <c>AddApplication</c> and <c>AddInfrastructure</c>, so persistence and
/// messaging tests exercise the production wiring with a controllable tenant, user and clock. Hosted services
/// (the outbox dispatcher) do not run: there is no host.
/// </summary>
internal static class PersistenceServices
{
    /// <summary>Valid SMTP settings that are never used unless a test sends e-mail.</summary>
    public static IReadOnlyDictionary<string, string?> UnusedSmtpSettings { get; } = new Dictionary<string, string?>
    {
        ["Email:Smtp:Host"] = "smtp.invalid",
        ["Email:Smtp:FromAddress"] = "noreply@tests.boilerplate.local",
    };

    public static ServiceProvider Create(
        string connectionString,
        TimeProvider? timeProvider = null,
        Action<IServiceCollection>? configure = null,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = connectionString })
            .AddInMemoryCollection(UnusedSmtpSettings)
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
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
        services.AddApplication();
        services.AddInfrastructure();
        configure?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>A scope acting for <paramref name="tenantId"/> and <paramref name="userId"/> (either may be none).</summary>
    public static AsyncServiceScope CreateScope(this IServiceProvider services, Guid? tenantId, string? userId = null)
    {
        var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestTenantContext>().ExplicitTenantId = tenantId;
        scope.ServiceProvider.GetRequiredService<TestCurrentUser>().UserId = userId;
        return scope;
    }
}
