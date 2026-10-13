using System.Net;
using System.Net.Http.Json;
using Boilerplate.Application.Features.Accounts;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests.Accounts;

/// <summary>Refresh token rotation, reuse detection, sign-out, password reset and key rotation (ADR-0025).</summary>
public sealed class SessionTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncDisposable
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly AccountsApi _api = AccountsApi.Start(factory);

    [Fact]
    public async Task Refresh_returns_a_new_pair_and_the_new_refresh_token_works()
    {
        var (_, tokens) = await SignedInAsync();

        var refreshed = await RefreshAsync(tokens.RefreshToken);
        var again = await RefreshAsync(refreshed.RefreshToken);

        refreshed.RefreshToken.ShouldNotBe(tokens.RefreshToken);
        again.RefreshToken.ShouldNotBe(refreshed.RefreshToken);
        (await _api.MeAsync(again.AccessToken, _cancellationToken)).UserId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Reusing_a_refresh_token_revokes_the_whole_session()
    {
        var (_, tokens) = await SignedInAsync();
        var refreshed = await RefreshAsync(tokens.RefreshToken);

        // The old token presented again: someone kept a copy.
        using var replay = await PostRefreshAsync(tokens.RefreshToken);
        using var newest = await PostRefreshAsync(refreshed.RefreshToken);

        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        newest.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Concurrent_refreshes_with_one_token_succeed_at_most_once()
    {
        var (_, tokens) = await SignedInAsync();

        var responses = await Task.WhenAll(PostRefreshAsync(tokens.RefreshToken), PostRefreshAsync(tokens.RefreshToken));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            responses.Count(response => response.StatusCode == HttpStatusCode.Unauthorized).ShouldBe(1);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Logout_ends_the_session_and_ignores_unknown_tokens()
    {
        var (_, tokens) = await SignedInAsync();

        using var logout = await _api.PostAsync("logout", new { refreshToken = tokens.RefreshToken }, _cancellationToken);
        using var unknown = await _api.PostAsync("logout", new { refreshToken = "not-a-token" }, _cancellationToken);
        using var refresh = await PostRefreshAsync(tokens.RefreshToken);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Password_reset_sets_the_password_unlocks_and_signs_out_everywhere()
    {
        var (email, tokens) = await SignedInAsync();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failed = await _api.PostAsync("login", new { email, password = "not the password!" }, _cancellationToken);
        }

        using var forgot = await _api.PostAsync("forgot-password", new { email }, _cancellationToken);
        var resetEmail = await _api.LastEmailToAsync(email, _cancellationToken);
        var token = AccountsApi.TokenFrom(resetEmail);
        using var reset = await _api.PostAsync(
            "reset-password", new { token, newPassword = "a brand new passphrase" }, _cancellationToken);
        using var reused = await _api.PostAsync(
            "reset-password", new { token, newPassword = "yet another passphrase" }, _cancellationToken);

        forgot.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        resetEmail.Subject.ShouldBe("Reset your password");
        resetEmail.TextBody.ShouldContain($"{AccountsApi.ResetUrl}?token=");
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        reused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var oldPassword = await _api.PostAsync("login", new { email, password = AccountsApi.Password }, _cancellationToken);
        oldPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await _api.LoginAsync(email, "a brand new passphrase", tenantId: null, _cancellationToken);
        using var oldSession = await PostRefreshAsync(tokens.RefreshToken);
        oldSession.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_address_answers_the_same_and_sends_nothing()
    {
        var email = AccountsApi.NewEmail();

        using var response = await _api.PostAsync("forgot-password", new { email }, _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await _api.EmailsToAsync(email, _cancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Tokens_signed_with_a_previous_key_stay_valid_after_key_rotation()
    {
        var oldKey = AccountsApi.NewSigningKey();
        await using var before = AccountsApi.Start(factory, new Dictionary<string, string?> { ["Accounts:Tokens:SigningKey"] = oldKey });
        var email = AccountsApi.NewEmail();
        await before.CreateConfirmedAccountAsync(email, _cancellationToken);
        var issuedBefore = await before.LoginAsync(email, AccountsApi.Password, tenantId: null, _cancellationToken);

        await using var rotated = AccountsApi.Start(factory, new Dictionary<string, string?>
        {
            ["Accounts:Tokens:SigningKey"] = AccountsApi.NewSigningKey(),
            ["Accounts:Tokens:PreviousSigningKeys:0"] = oldKey,
        });
        await using var forgotten = AccountsApi.Start(factory);

        (await rotated.MeAsync(issuedBefore.AccessToken, _cancellationToken)).UserId.ShouldNotBeNullOrEmpty();
        using var client = forgotten.Host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", issuedBefore.AccessToken);
        using var rejected = await client.GetAsync("/api/v1/me", _cancellationToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private async Task<(string Email, TokenResponse Tokens)> SignedInAsync()
    {
        var email = AccountsApi.NewEmail();
        await _api.CreateConfirmedAccountAsync(email, _cancellationToken);
        return (email, await _api.LoginAsync(email, AccountsApi.Password, tenantId: null, _cancellationToken));
    }

    private Task<HttpResponseMessage> PostRefreshAsync(string refreshToken) =>
        _api.PostAsync("refresh", new { refreshToken }, _cancellationToken);

    private async Task<TokenResponse> RefreshAsync(string refreshToken)
    {
        using var response = await PostRefreshAsync(refreshToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<TokenResponse>(_cancellationToken))!;
    }
}
