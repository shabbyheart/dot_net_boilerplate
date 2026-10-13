using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Boilerplate.Api.Controllers;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests;

/// <summary>Token validation, deny-by-default authorization and identity resolution (ADR-0016, ADR-0010).</summary>
public sealed class AuthenticationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string MePath = "/api/v1/me";

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Request_without_a_token_returns_401_problem()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(MePath, _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        response.Headers.WwwAuthenticate.ShouldContain(header => header.Scheme == "Bearer");
    }

    [Fact]
    public async Task Valid_token_resolves_user_and_tenant()
    {
        var tenantId = Guid.NewGuid();

        using var response = await GetMeAsync(TestTokens.Create(subject: "user-42", tenantId: tenantId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>(_cancellationToken);
        me.ShouldBe(new MeResponse("user-42", tenantId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task Missing_or_malformed_tenant_claim_means_no_tenant(string? tenantClaim)
    {
        using var response = await GetMeAsync(TestTokens.Create(tenantClaim: tenantClaim));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<MeResponse>(_cancellationToken))!.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task Token_without_subject_is_forbidden()
    {
        using var response = await GetMeAsync(TestTokens.Create(subject: null));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public Task Expired_token_is_rejected() =>
        AssertUnauthorizedAsync(TestTokens.Create(expires: DateTime.UtcNow.AddMinutes(-5)));

    [Fact]
    public Task Token_for_another_audience_is_rejected() =>
        AssertUnauthorizedAsync(TestTokens.Create(audience: "another-api"));

    [Fact]
    public Task Token_from_another_issuer_is_rejected() =>
        AssertUnauthorizedAsync(TestTokens.Create(issuer: "https://attacker.example"));

    [Fact]
    public Task Token_signed_with_another_key_is_rejected() =>
        AssertUnauthorizedAsync(TestTokens.Create(signingKey: RandomNumberGenerator.GetBytes(32)));

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/openapi/v1.json")]
    public async Task Operational_and_documentation_endpoints_allow_anonymous_access(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Openapi_document_describes_the_me_endpoint()
    {
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", _cancellationToken));

        document.RootElement.GetProperty("paths").TryGetProperty(MePath, out _).ShouldBeTrue();
    }

    private async Task<HttpResponseMessage> GetMeAsync(string token)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync(MePath, _cancellationToken);
    }

    private async Task AssertUnauthorizedAsync(string token)
    {
        using var response = await GetMeAsync(token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
