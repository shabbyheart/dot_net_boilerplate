using Npgsql;
using OpenTelemetry.Trace;

namespace Boilerplate.Infrastructure.Observability;

/// <summary>Tracing of Infrastructure dependencies (ADR-0023), kept here so the API never references Npgsql.</summary>
public static class InfrastructureInstrumentation
{
    public static TracerProviderBuilder AddInfrastructureInstrumentation(this TracerProviderBuilder tracing) =>
        tracing.AddNpgsql();
}
