using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Boilerplate.Application.Features.Projects;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests;

/// <summary>
/// Kestrel rejects request bodies over 1 MB with 413 (ADR-0024). Needs a real server process: the in-memory test
/// server does not enforce Kestrel limits.
/// </summary>
public sealed class RequestSizeTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly AuthorizationData _data = new(postgres);

    [Fact]
    public async Task Body_over_the_limit_is_rejected_with_413_problem()
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        await _data.GrantAsync(tenantId, "uploader", [ProjectPermissions.Manage], _cancellationToken);

        var settings = new Dictionary<string, string?>(TestTokens.ApiSettings)
        {
            ["Database:ConnectionString"] = postgres.ConnectionString,
            ["Outbox:Enabled"] = "false",
        };
        foreach (var (key, value) in PersistenceServices.UnusedSmtpSettings)
        {
            settings[key] = value;
        }

        await using var server = await ApiServerProcess.StartAsync(settings, _cancellationToken);
        using var client = new HttpClient { BaseAddress = server.BaseAddress };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create("uploader", tenantId));

        using var small = await client.PostAsJsonAsync("/api/v1/projects", new { name = "Small" }, _cancellationToken);
        using var large = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new { name = "Large", description = new string('x', 2 * 1024 * 1024) },
            _cancellationToken);

        small.StatusCode.ShouldBe(HttpStatusCode.Created);
        large.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        large.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    public ValueTask DisposeAsync() => _data.DisposeAsync();
}
