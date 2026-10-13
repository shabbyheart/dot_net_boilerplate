using System.Net;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests;

public sealed class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Live_returns_200_healthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(cancellationToken)).ShouldBe("Healthy");
    }
}
