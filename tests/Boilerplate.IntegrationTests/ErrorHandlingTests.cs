using System.Net;
using System.Text.Json;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests;

/// <summary>The error contract from ADR-0006.</summary>
public sealed class ErrorHandlingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Unhandled_exception_returns_generic_500_problem_without_internals()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(ThrowingRoutesStartupFilter.UnhandledPath, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        body.ShouldNotContain(ThrowingRoutesStartupFilter.SensitiveMessage);
        body.ShouldNotContain(nameof(InvalidOperationException));

        using var problem = JsonDocument.Parse(body);
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(500);
        problem.RootElement.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
        problem.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        problem.RootElement.TryGetProperty("detail", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Not_found_exception_returns_404_problem_with_detail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(ThrowingRoutesStartupFilter.NotFoundPath, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
        problem.RootElement.GetProperty("detail").GetString().ShouldBe("Widget '42' was not found.");
        problem.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Unknown_route_returns_404_problem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/does-not-exist", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }
}
