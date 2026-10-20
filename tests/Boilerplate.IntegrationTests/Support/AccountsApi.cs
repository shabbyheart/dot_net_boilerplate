using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Boilerplate.Api.Controllers;
using Boilerplate.Application.Common.Email;
using Boilerplate.Application.Features.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// The API with local accounts enabled (ADR-0025), plus helpers for the account flows. E-mails are read from the
/// outbox table: the dispatcher is off in tests, so queued messages keep their payload.
/// </summary>
internal sealed class AccountsApi(WebApplicationFactory<Program> host) : IAsyncDisposable
{
    public const string Issuer = "https://accounts.tests.boilerplate.local";
    public const string Audience = "boilerplate-api";
    public const string ConfirmationUrl = "https://app.example/confirm-email";
    public const string ResetUrl = "https://app.example/reset-password";
    public const string Password = "correct horse battery staple";

    public WebApplicationFactory<Program> Host => host;

    public static string NewSigningKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static Dictionary<string, string?> Settings(string signingKey) => new()
    {
        ["Accounts:Enabled"] = "true",
        ["Accounts:EmailConfirmationUrl"] = ConfirmationUrl,
        ["Accounts:PasswordResetUrl"] = ResetUrl,
        ["Accounts:Tokens:Issuer"] = Issuer,
        ["Accounts:Tokens:Audience"] = Audience,
        ["Accounts:Tokens:SigningKey"] = signingKey,

        // All anonymous requests of a test host share one partition; tests of the limits set their own values.
        ["RateLimiting:PermitLimit"] = "10000",
        ["RateLimiting:AccountsPermitLimit"] = "10000",
    };

    public static AccountsApi Start(ApiFactory factory, IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var settings = Settings(NewSigningKey());
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        return new AccountsApi(factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings))));
    }

    public static string NewEmail() => $"{Guid.NewGuid():N}@tests.example";

    public async Task<HttpResponseMessage> PostAsync(string action, object body, CancellationToken cancellationToken)
    {
        using var client = host.CreateClient();
        return await client.PostAsJsonAsync($"/api/v1/account/{action}", body, cancellationToken);
    }

    public async Task RegisterAsync(string email, string password, CancellationToken cancellationToken)
    {
        using var response = await PostAsync("register", new { email, password }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    /// <summary>Registers, confirms and signs in once; returns the new user's id (the token subject).</summary>
    public async Task<string> CreateConfirmedAccountAsync(string email, CancellationToken cancellationToken)
    {
        await RegisterAsync(email, Password, cancellationToken);
        using var confirmed = await PostAsync(
            "confirm-email", new { token = TokenFrom(await LastEmailToAsync(email, cancellationToken)) }, cancellationToken);
        confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var tokens = await LoginAsync(email, Password, tenantId: null, cancellationToken);
        return (await MeAsync(tokens.AccessToken, cancellationToken)).UserId;
    }

    public async Task<TokenResponse> LoginAsync(string email, string password, Guid? tenantId, CancellationToken cancellationToken)
    {
        using var response = await PostAsync("login", new { email, password, tenantId }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancellationToken));
        return (await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken))!;
    }

    public async Task<MeResponse> MeAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.GetAsync("/api/v1/me", cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<MeResponse>(cancellationToken))!;
    }

    /// <summary>The e-mails queued for <paramref name="to"/>, oldest first.</summary>
    public Task<List<EmailMessage>> EmailsToAsync(string to, CancellationToken cancellationToken) =>
        QueuedEmails.ToAsync(host.Services, to, cancellationToken);

    public async Task<EmailMessage> LastEmailToAsync(string to, CancellationToken cancellationToken) =>
        (await EmailsToAsync(to, cancellationToken)).Last();

    /// <summary>The token from the link in an account e-mail.</summary>
    public static string TokenFrom(EmailMessage email) => QueuedEmails.TokenFrom(email);

    public ValueTask DisposeAsync() => host.DisposeAsync();
}
