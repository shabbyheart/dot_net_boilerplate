using System.Net;
using System.Text.Json;
using Boilerplate.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;

namespace Boilerplate.IntegrationTests;

/// <summary>OpenAPI document and reference UI are Development-only and anonymous (ADR-0013, ADR-0016).</summary>
public sealed class OpenApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Development_serves_openapi_document()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        document.RootElement.GetProperty("openapi").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Development_serves_reference_ui()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/scalar", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task Production_does_not_expose_documentation(string path)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        // Not mapped: anonymous callers are refused like any unknown path, authenticated callers get 404.
        using var anonymous = production.CreateClient();
        using var anonymousResponse = await anonymous.GetAsync(path, cancellationToken);
        anonymousResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var authenticated = production.CreateAuthenticatedClient();
        using var authenticatedResponse = await authenticated.GetAsync(path, cancellationToken);
        authenticatedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
