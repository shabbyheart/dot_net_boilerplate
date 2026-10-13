using System.Net;
using Boilerplate.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests;

/// <summary>Security headers, CORS, rate limiting, trusted proxies and HSTS (ADR-0024).</summary>
public sealed class HardeningTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/health/live", false)]
    [InlineData("/api/v1/me", false)]
    [InlineData(ThrowingRoutesStartupFilter.UnhandledPath, true)]
    public async Task Security_headers_are_on_success_and_error_responses(string path, bool authenticated)
    {
        using var client = authenticated ? factory.CreateAuthenticatedClient() : factory.CreateClient();

        using var response = await client.GetAsync(path, _cancellationToken);

        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        response.Headers.GetValues("Content-Security-Policy").ShouldBe(["default-src 'none'; frame-ancestors 'none'"]);
    }

    [Fact]
    public async Task Api_reference_page_has_no_content_security_policy()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/scalar", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Content-Security-Policy").ShouldBeFalse();
    }

    [Fact]
    public async Task Cors_allows_only_configured_origins()
    {
        await using var api = With(new() { ["Cors:AllowedOrigins:0"] = "https://app.example" });
        using var client = api.CreateClient();

        using var allowed = await client.SendAsync(Preflight("https://app.example"), _cancellationToken);
        using var other = await client.SendAsync(Preflight("https://evil.example"), _cancellationToken);

        allowed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        allowed.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["https://app.example"]);
        other.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task Cors_allows_no_origin_by_default()
    {
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Preflight("https://app.example"), _cancellationToken);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task Caller_over_the_limit_gets_429_problem_with_retry_after_and_others_are_unaffected()
    {
        await using var api = With(new() { ["RateLimiting:PermitLimit"] = "3", ["RateLimiting:Window"] = "00:01:00" });
        using var limited = api.CreateAuthenticatedClient(TestTokens.Create("busy-user"));
        using var other = api.CreateAuthenticatedClient(TestTokens.Create("quiet-user"));

        for (var i = 0; i < 3; i++)
        {
            (await StatusAsync(limited.GetAsync("/api/v1/me", _cancellationToken))).ShouldBe(HttpStatusCode.OK);
        }

        using var rejected = await limited.GetAsync("/api/v1/me", _cancellationToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        rejected.Headers.Contains("Retry-After").ShouldBeTrue();

        (await StatusAsync(other.GetAsync("/api/v1/me", _cancellationToken))).ShouldBe(HttpStatusCode.OK);
        for (var i = 0; i < 5; i++)
        {
            (await StatusAsync(limited.GetAsync("/health/live", _cancellationToken))).ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Anonymous_callers_are_limited_per_client_ip_from_a_trusted_proxy()
    {
        await using var api = With(
            new()
            {
                ["RateLimiting:PermitLimit"] = "2",
                ["RateLimiting:Window"] = "00:01:00",
                ["ForwardedHeaders:KnownNetworks:0"] = "10.0.0.0/8",
            },
            withRemoteIp: true);
        using var client = api.CreateClient();

        (await StatusAsync(client.SendAsync(FromProxy("/api/v1/me", forwardedFor: "203.0.113.1"), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(client.SendAsync(FromProxy("/api/v1/me", forwardedFor: "203.0.113.1"), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(client.SendAsync(FromProxy("/api/v1/me", forwardedFor: "203.0.113.1"), _cancellationToken)))
            .ShouldBe(HttpStatusCode.TooManyRequests);

        // Another client behind the same proxy has its own budget.
        (await StatusAsync(client.SendAsync(FromProxy("/api/v1/me", forwardedFor: "203.0.113.2"), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Hsts_is_sent_for_https_forwarded_by_a_trusted_proxy_only()
    {
        await using var api = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            Configure(builder, new() { ["ForwardedHeaders:KnownNetworks:0"] = "10.0.0.0/8" }, withRemoteIp: true);
        });
        using var client = api.CreateClient();

        // The HSTS middleware never sends the header to localhost, so use a public-looking host name.
        var trustedRequest = FromProxy("/health/live", proto: "https");
        trustedRequest.Headers.Host = "api.example.com";
        var untrustedRequest = FromProxy("/health/live", proto: "https", proxyIp: "192.0.2.10");
        untrustedRequest.Headers.Host = "api.example.com";

        using var trusted = await client.SendAsync(trustedRequest, _cancellationToken);
        using var untrusted = await client.SendAsync(untrustedRequest, _cancellationToken);

        trusted.Headers.GetValues("Strict-Transport-Security").Single().ShouldStartWith("max-age=31536000");
        untrusted.Headers.Contains("Strict-Transport-Security").ShouldBeFalse();
    }

    private WebApplicationFactory<Program> With(Dictionary<string, string?> settings, bool withRemoteIp = false) =>
        factory.WithWebHostBuilder(builder => Configure(builder, settings, withRemoteIp));

    private static void Configure(IWebHostBuilder builder, Dictionary<string, string?> settings, bool withRemoteIp)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        if (withRemoteIp)
        {
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, TestRemoteIpStartupFilter>());
        }
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/projects");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        return request;
    }

    private static HttpRequestMessage FromProxy(
        string path,
        string? forwardedFor = null,
        string? proto = null,
        string proxyIp = "10.1.2.3")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestRemoteIpStartupFilter.HeaderName, proxyIp);
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        if (proto is not null)
        {
            request.Headers.Add("X-Forwarded-Proto", proto);
        }

        return request;
    }

    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return response.StatusCode;
    }
}
