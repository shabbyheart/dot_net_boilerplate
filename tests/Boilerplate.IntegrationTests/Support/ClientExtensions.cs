using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Boilerplate.IntegrationTests.Support;

internal static class ClientExtensions
{
    /// <summary>A client that sends a valid bearer token (by default for <see cref="TestTokens.DefaultSubject"/>).</summary>
    public static HttpClient CreateAuthenticatedClient(this WebApplicationFactory<Program> factory, string? token = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token ?? TestTokens.Create());
        return client;
    }
}
