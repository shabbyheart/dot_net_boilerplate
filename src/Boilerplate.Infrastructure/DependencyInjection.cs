using Boilerplate.Application.Common.Persistence;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Boilerplate.Infrastructure;

/// <summary>
/// Registers Infrastructure services (ADR-0004). The host must also register implementations of
/// <see cref="Application.Common.Tenancy.ITenantContext"/> and <see cref="Application.Common.Security.ICurrentUser"/>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Health checks with this tag report whether the application can serve traffic (<c>/health/ready</c>).</summary>
    public const string ReadyHealthCheckTag = "ready";

    private const string DatabaseHealthCheckName = "database";

    private static readonly TimeSpan DatabaseHealthCheckTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<AuditInterceptor>();
        services.AddScoped<TenantStampingInterceptor>();

        services.AddDbContext<AppDbContext>(ConfigureDbContext);
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(DatabaseHealthCheckName, tags: [ReadyHealthCheckTag]);

        // Connection retries (EnableRetryOnFailure) would keep a failing check running for about a minute;
        // probes need a prompt answer, so the check gives up and reports Unhealthy after a fixed time.
        services.Configure<HealthCheckServiceOptions>(options =>
            options.Registrations.Single(registration => registration.Name == DatabaseHealthCheckName).Timeout =
                DatabaseHealthCheckTimeout);

        return services;
    }

    /// <summary>
    /// The single place that configures the provider, conventions and interceptors of an <see cref="AppDbContext"/>.
    /// Contexts derived from <see cref="AppDbContext"/> (tests) use it too, so they behave like production.
    /// </summary>
    public static void ConfigureDbContext(IServiceProvider services, DbContextOptionsBuilder options)
    {
        var databaseOptions = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        options
            .UseAppDatabase(databaseOptions.ConnectionString)
            .AddInterceptors(
                services.GetRequiredService<AuditInterceptor>(),
                services.GetRequiredService<TenantStampingInterceptor>());
    }

    /// <summary>Provider and naming conventions (ADR-0009, ADR-0011), shared with design-time tooling.</summary>
    internal static DbContextOptionsBuilder UseAppDatabase(this DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
            .UseSnakeCaseNamingConvention();
}
