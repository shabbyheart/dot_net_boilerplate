using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Boilerplate.Application.Features.Accounts;
using Boilerplate.Application.Features.Accounts.Login;
using Boilerplate.Application.Features.Accounts.TwoFactor;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests.Accounts;

/// <summary>Two-factor sign-in for local accounts: enrolment, sign-in, recovery codes and lockout (ADR-0031).</summary>
public sealed class TwoFactorTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncDisposable
{
    private const string TwoFactor = "/api/v1/account/two-factor";

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly AccountsApi _api = AccountsApi.Start(factory);

    [Fact]
    public async Task Enrolment_turns_two_factor_on_and_returns_recovery_codes_once()
    {
        var account = await NewAccountAsync();

        var status = await account.Client.GetFromJsonAsync<TwoFactorStatusResponse>(TwoFactor, _cancellationToken);
        status.ShouldBe(new TwoFactorStatusResponse(Enabled: false, RecoveryCodesLeft: 0));

        var setup = await SetupAsync(account.Client);
        setup.AuthenticatorUri.ShouldStartWith($"otpauth://totp/Boilerplate:{Uri.EscapeDataString(account.Email)}?secret={setup.SharedKey}");

        using var enabled = await account.Client.PostAsJsonAsync(
            $"{TwoFactor}/enable", new EnableTwoFactorRequest(AccountsApi.Password, Totp.Code(setup.SharedKey)), _cancellationToken);

        enabled.StatusCode.ShouldBe(HttpStatusCode.OK);
        enabled.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var codes = (await enabled.Content.ReadFromJsonAsync<RecoveryCodesResponse>(_cancellationToken))!.RecoveryCodes;
        codes.Count.ShouldBe(RecoveryCodes.Count);
        codes.Distinct().Count().ShouldBe(RecoveryCodes.Count);
        (await account.Client.GetFromJsonAsync<TwoFactorStatusResponse>(TwoFactor, _cancellationToken))
            .ShouldBe(new TwoFactorStatusResponse(Enabled: true, RecoveryCodesLeft: RecoveryCodes.Count));
        (await _api.LastEmailToAsync(account.Email, _cancellationToken)).Subject.ShouldBe("Two-factor sign-in is on");

        // A new key would lock the user out; it must be switched off first.
        (await StatusAsync(account.Client.PostAsync($"{TwoFactor}/setup", content: null, _cancellationToken))).ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Enabling_needs_the_password_and_a_code_from_the_new_key()
    {
        var account = await NewAccountAsync();
        var setup = await SetupAsync(account.Client);

        (await StatusAsync(account.Client.PostAsJsonAsync(
            $"{TwoFactor}/enable", new EnableTwoFactorRequest("not the password!", Totp.Code(setup.SharedKey)), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(account.Client.PostAsJsonAsync(
            $"{TwoFactor}/enable", new EnableTwoFactorRequest(AccountsApi.Password, Totp.WrongCode(setup.SharedKey)), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Forbidden);

        (await account.Client.GetFromJsonAsync<TwoFactorStatusResponse>(TwoFactor, _cancellationToken))!.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task Sign_in_asks_for_a_code_and_accepts_a_code_or_a_recovery_code_once()
    {
        var account = await NewAccountAsync();
        var (key, codes) = await EnableAsync(account.Client);

        using var withoutCode = await LoginAsync(account.Email);
        withoutCode.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var problem = await ProblemAsync(withoutCode);
        problem.GetProperty("twoFactorRequired").GetBoolean().ShouldBeTrue();
        problem.GetProperty("detail").GetString().ShouldBe(LoginHandler.TwoFactorRequiredMessage);

        using var wrongCode = await LoginAsync(account.Email, twoFactorCode: Totp.WrongCode(key));
        wrongCode.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var failed = await ProblemAsync(wrongCode);
        failed.TryGetProperty("twoFactorRequired", out _).ShouldBeFalse();
        failed.GetProperty("detail").GetString().ShouldBe(LoginHandler.FailedMessage);

        using var withCode = await LoginAsync(account.Email, twoFactorCode: Totp.Code(key));
        withCode.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Recovery codes work in any spelling, once.
        using var withRecoveryCode = await LoginAsync(account.Email, recoveryCode: codes[0].ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal));
        withRecoveryCode.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var reused = await LoginAsync(account.Email, recoveryCode: codes[0]);
        reused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var tokens = (await withCode.Content.ReadFromJsonAsync<TokenResponse>(_cancellationToken))!;
        using var client = ClientFor(tokens.AccessToken);
        (await client.GetFromJsonAsync<TwoFactorStatusResponse>(TwoFactor, _cancellationToken))!.RecoveryCodesLeft
            .ShouldBe(RecoveryCodes.Count - 1);
    }

    [Fact]
    public async Task Asking_for_a_code_does_not_reset_the_failed_attempts()
    {
        var account = await NewAccountAsync();
        var (key, _) = await EnableAsync(account.Client);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var wrong = await LoginAsync(account.Email, twoFactorCode: Totp.WrongCode(key));
            wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

            // The right password alone must not clear the count, or codes could be guessed forever.
            using var passwordOnly = await LoginAsync(account.Email);
            (await ProblemAsync(passwordOnly)).GetProperty("twoFactorRequired").GetBoolean().ShouldBeTrue();
        }

        using var fifth = await LoginAsync(account.Email, twoFactorCode: Totp.WrongCode(key));
        using var locked = await LoginAsync(account.Email, twoFactorCode: Totp.Code(key));

        locked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ProblemAsync(locked)).GetProperty("detail").GetString().ShouldBe(LoginHandler.FailedMessage);
    }

    [Fact]
    public async Task Disabling_needs_the_password_and_a_code_or_recovery_code()
    {
        var account = await NewAccountAsync();
        var (_, codes) = await EnableAsync(account.Client);

        (await StatusAsync(account.Client.PostAsJsonAsync(
            $"{TwoFactor}/disable", new DisableTwoFactorRequest("not the password!", null, codes[0]), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(account.Client.PostAsJsonAsync(
            $"{TwoFactor}/disable", new DisableTwoFactorRequest(AccountsApi.Password, null, "AAAA-AAAA-AAAA-AAAA"), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Forbidden);

        (await StatusAsync(account.Client.PostAsJsonAsync(
            $"{TwoFactor}/disable", new DisableTwoFactorRequest(AccountsApi.Password, null, codes[1]), _cancellationToken)))
            .ShouldBe(HttpStatusCode.NoContent);

        (await account.Client.GetFromJsonAsync<TwoFactorStatusResponse>(TwoFactor, _cancellationToken))
            .ShouldBe(new TwoFactorStatusResponse(Enabled: false, RecoveryCodesLeft: 0));
        (await StatusAsync(LoginAsync(account.Email))).ShouldBe(HttpStatusCode.OK);
        (await _api.LastEmailToAsync(account.Email, _cancellationToken)).Subject.ShouldBe("Two-factor sign-in is off");
    }

    [Fact]
    public async Task Renewed_recovery_codes_replace_the_old_ones()
    {
        var account = await NewAccountAsync();
        var (key, oldCodes) = await EnableAsync(account.Client);

        using var renewed = await account.Client.PostAsJsonAsync(
            $"{TwoFactor}/recovery-codes", new RenewRecoveryCodesRequest(AccountsApi.Password, Totp.Code(key)), _cancellationToken);

        renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var newCodes = (await renewed.Content.ReadFromJsonAsync<RecoveryCodesResponse>(_cancellationToken))!.RecoveryCodes;
        newCodes.ShouldNotContain(oldCodes[0]);
        (await StatusAsync(LoginAsync(account.Email, recoveryCode: oldCodes[0]))).ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(LoginAsync(account.Email, recoveryCode: newCodes[0]))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Changes_that_do_not_match_the_state_answer_409()
    {
        var account = await NewAccountAsync();

        (await StatusAsync(account.Client.PostAsJsonAsync(
            $"{TwoFactor}/disable", new DisableTwoFactorRequest(AccountsApi.Password, "123456", null), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Conflict);
        (await StatusAsync(account.Client.PostAsJsonAsync(
            $"{TwoFactor}/recovery-codes", new RenewRecoveryCodesRequest(AccountsApi.Password, "123456"), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Invalid_requests_return_400()
    {
        var account = await NewAccountAsync();

        (await StatusAsync(account.Client.PostAsJsonAsync(
            $"{TwoFactor}/enable", new { password = AccountsApi.Password, code = "abc" }, _cancellationToken)))
            .ShouldBe(HttpStatusCode.BadRequest);
        (await StatusAsync(account.Client.PostAsJsonAsync(
            $"{TwoFactor}/disable", new { password = AccountsApi.Password }, _cancellationToken)))
            .ShouldBe(HttpStatusCode.BadRequest);
        (await StatusAsync(LoginAsync(account.Email, twoFactorCode: "123456", recoveryCode: "AAAA-AAAA-AAAA-AAAA")))
            .ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Endpoints_need_a_signed_in_local_account()
    {
        using var anonymous = _api.Host.CreateClient();
        using var external = _api.Host.CreateAuthenticatedClient(TestTokens.Create($"external-{Guid.NewGuid():N}"));

        (await StatusAsync(anonymous.GetAsync(TwoFactor, _cancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(anonymous.PostAsync($"{TwoFactor}/setup", content: null, _cancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(external.GetAsync(TwoFactor, _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await StatusAsync(external.PostAsync($"{TwoFactor}/setup", content: null, _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoints_do_not_exist_unless_accounts_are_enabled()
    {
        using var client = factory.CreateAuthenticatedClient(TestTokens.Create($"user-{Guid.NewGuid():N}"));

        (await StatusAsync(client.GetAsync(TwoFactor, _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
    }

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private async Task<Account> NewAccountAsync()
    {
        var email = AccountsApi.NewEmail();
        await _api.CreateConfirmedAccountAsync(email, _cancellationToken);
        var tokens = await _api.LoginAsync(email, AccountsApi.Password, tenantId: null, _cancellationToken);
        return new Account(email, ClientFor(tokens.AccessToken));
    }

    private HttpClient ClientFor(string accessToken)
    {
        var client = _api.Host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<AuthenticatorSetupResponse> SetupAsync(HttpClient client)
    {
        using var response = await client.PostAsync($"{TwoFactor}/setup", content: null, _cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(_cancellationToken));
        return (await response.Content.ReadFromJsonAsync<AuthenticatorSetupResponse>(_cancellationToken))!;
    }

    private async Task<(string Key, IReadOnlyList<string> RecoveryCodes)> EnableAsync(HttpClient client)
    {
        var setup = await SetupAsync(client);
        using var response = await client.PostAsJsonAsync(
            $"{TwoFactor}/enable", new EnableTwoFactorRequest(AccountsApi.Password, Totp.Code(setup.SharedKey)), _cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(_cancellationToken));
        return (setup.SharedKey, (await response.Content.ReadFromJsonAsync<RecoveryCodesResponse>(_cancellationToken))!.RecoveryCodes);
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string? twoFactorCode = null, string? recoveryCode = null) =>
        _api.PostAsync("login", new { email, password = AccountsApi.Password, twoFactorCode, recoveryCode }, _cancellationToken);

    private async Task<JsonElement> ProblemAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(_cancellationToken);

    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return response.StatusCode;
    }

    private sealed record Account(string Email, HttpClient Client);
}
