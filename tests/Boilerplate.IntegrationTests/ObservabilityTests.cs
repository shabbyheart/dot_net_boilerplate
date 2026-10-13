using System.Diagnostics;
using System.Text.Json;
using Boilerplate.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Boilerplate.IntegrationTests;

/// <summary>
/// Traces cover requests and database commands, and error responses carry the trace (ADR-0023).
/// Span listeners are process-wide, so each test sends its own W3C trace id and looks only at spans of that trace.
/// </summary>
public sealed class ObservabilityTests(ApiFactory factory, PostgresFixture postgres) : IClassFixture<ApiFactory>, IAsyncDisposable
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly List<Activity> _spans = [];
    private readonly AuthorizationData _data = new(postgres);

    [Fact]
    public async Task Request_and_its_database_commands_are_traced_in_the_callers_trace()
    {
        await using var api = WithInMemoryTracing();
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        await _data.GrantAsync(tenantId, "traced-user", ["projects.read"], _cancellationToken);
        using var client = api.CreateAuthenticatedClient(TestTokens.Create("traced-user", tenantId));
        var traceId = ActivityTraceId.CreateRandom();

        using var response = await client.SendAsync(Traced(HttpMethod.Get, "/api/v1/projects", traceId), _cancellationToken);
        response.EnsureSuccessStatusCode();

        await WaitForSpanAsync(span => span.TraceId == traceId && span.Kind == ActivityKind.Server);
        var database = await WaitForSpanAsync(span => span.TraceId == traceId && span.Source.Name == "Npgsql");
        database.Kind.ShouldBe(ActivityKind.Client);
    }

    [Fact]
    public async Task Error_response_trace_id_identifies_the_request_trace()
    {
        await using var api = WithInMemoryTracing();
        using var client = api.CreateAuthenticatedClient();
        var traceId = ActivityTraceId.CreateRandom();

        using var response = await client.SendAsync(Traced(HttpMethod.Get, ThrowingRoutesStartupFilter.UnhandledPath, traceId), _cancellationToken);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_cancellationToken));

        problem.RootElement.GetProperty("traceId").GetString()!.ShouldContain(traceId.ToHexString());
        await WaitForSpanAsync(span => span.TraceId == traceId && span.Kind == ActivityKind.Server);
    }

    [Fact]
    public async Task Health_probes_are_not_traced()
    {
        await using var api = WithInMemoryTracing();
        using var client = api.CreateClient();
        var probeTrace = ActivityTraceId.CreateRandom();
        var apiTrace = ActivityTraceId.CreateRandom();

        (await client.SendAsync(Traced(HttpMethod.Get, "/health/live", probeTrace), _cancellationToken)).Dispose();
        (await client.SendAsync(Traced(HttpMethod.Get, "/api/v1/me", apiTrace), _cancellationToken)).Dispose();

        // Once the later request's span is exported, the earlier probe's span would have been too.
        await WaitForSpanAsync(span => span.TraceId == apiTrace && span.Kind == ActivityKind.Server);
        SpansSnapshot().ShouldNotContain(span => span.TraceId == probeTrace);
    }

    public ValueTask DisposeAsync() => _data.DisposeAsync();

    private static HttpRequestMessage Traced(HttpMethod method, string path, ActivityTraceId traceId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("traceparent", $"00-{traceId.ToHexString()}-{ActivitySpanId.CreateRandom().ToHexString()}-01");
        return request;
    }

    private WebApplicationFactory<Program> WithInMemoryTracing() =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(_spans))));

    // A server span is exported when the request completes, which can be just after the client has the response.
    private async Task<Activity> WaitForSpanAsync(Func<Activity, bool> predicate)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var match = SpansSnapshot().FirstOrDefault(predicate);
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), _cancellationToken);
        }

        throw new TimeoutException("Expected span was not exported.");
    }

    // The exporter appends from another thread without locking; retry if a copy coincides with an append.
    private Activity[] SpansSnapshot()
    {
        while (true)
        {
            try
            {
                return [.. _spans];
            }
            catch (InvalidOperationException)
            {
                Thread.Yield();
            }
            catch (ArgumentException)
            {
                Thread.Yield();
            }
        }
    }
}
