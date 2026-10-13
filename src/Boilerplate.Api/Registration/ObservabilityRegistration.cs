using Boilerplate.Infrastructure.Observability;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Boilerplate.Api.Registration;

/// <summary>
/// OpenTelemetry tracing and metrics (ADR-0023). Export is on only when an OTLP endpoint is configured.
/// In the Registration namespace because it composes Infrastructure instrumentation (ADR-0001).
/// </summary>
internal static class ObservabilityRegistration
{
    public const string DefaultServiceName = "boilerplate-api";

    public static IServiceCollection AddApiObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = configuration["OTEL_SERVICE_NAME"] is { Length: > 0 } configured ? configured : DefaultServiceName;

        // Health probes are not traced: they would dominate trace volume.
        var openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = IsNotHealthProbe)
                .AddHttpClientInstrumentation()
                .AddInfrastructureInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        // Standard variables: OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_PROTOCOL, OTEL_EXPORTER_OTLP_HEADERS.
        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            openTelemetry.UseOtlpExporter();
        }

        return services;
    }

    private static bool IsNotHealthProbe(HttpContext context) =>
        !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
}
