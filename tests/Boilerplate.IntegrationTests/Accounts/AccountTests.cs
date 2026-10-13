using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Boilerplate.Application.Features.Accounts;
using Boilerplate.Application.Features.Accounts.Login;
using Boilerplate.Application.Features.Accounts.Refresh;
using Boilerplate.Application.Features.Roles;
using Boilerplate.Domain.Accounts;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests.Accounts;

/// <summary>Registration, confirmation, sign-in, lockout and tenant selection for local accounts (ADR-0025).</summary>
public sealed class AccountTests(ApiFactory factory, PostgresFixture postgres) : IClassFixture<ApiFactory>, IAsyncDisposable
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly AccountsApi _api = AccountsApi.Start(factory);
    private readonly AuthorizationData _data = new(postgres);

    [Fact]
    public async Task Registered_and_confirmed_user_signs_in_with_a_token_the_api_accepts()
    {
        var email = AccountsApi.NewEmail();
        await _api.RegisterAsync(email, AccountsApi.Password, _cancellationToken);

        var confirmation = await _api.LastEmailToAsync(email, _cancellationToken);
        confirmation.Subject.ShouldBe("Confirm your e-mail address");
        confirmation.TextBody.ShouldContain($"{AccountsApi.ConfirmationUrl}?token=");

        using var confirmed = await _api.PostAsync(
            "confirm-email", new { token = AccountsApi.TokenFrom(confirmation) }, _cancellationToken);
        confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var login = await _api.PostAsync("login", new { email, password = AccountsApi.Password }, _cancellationToken);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        login.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>(_cancellationToken))!;
        tokens.TokenType.ShouldBe("Bearer");
        tokens.AccessTokenExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        tokens.RefreshTokenExpiresAt.ShouldBeGreaterThan(tokens.AccessTokenExpiresAt);

        var me = await _api.MeAsync(tokens.AccessToken, _cancellationToken);
        Guid.TryParse(me.UserId, out _).ShouldBeTrue();
        me.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task Unconfirmed_address_cannot_sign_in()
    {
        var email = AccountsApi.NewEmail();
        await _api.RegisterAsync(email, AccountsApi.Password, _cancellationToken);

        using var response = await _api.PostAsync("login", new { email, password = AccountsApi.Password }, _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_address_get_the_same_answer()
    {
        var email = AccountsApi.NewEmail();
        await _api.CreateConfirmedAccountAsync(email, _cancellationToken);

        using var wrongPassword = await _api.PostAsync("login", new { email, password = "not the password!" }, _cancellationToken);
        using var unknown = await _api.PostAsync(
            "login", new { email = AccountsApi.NewEmail(), password = AccountsApi.Password }, _cancellationToken);

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknown.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        wrongPassword.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await DetailAsync(wrongPassword)).ShouldBe(LoginHandler.FailedMessage);
        (await DetailAsync(unknown)).ShouldBe(LoginHandler.FailedMessage);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_right_password()
    {
        var email = AccountsApi.NewEmail();
        await _api.CreateConfirmedAccountAsync(email, _cancellationToken);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failed = await _api.PostAsync("login", new { email, password = "not the password!" }, _cancellationToken);
            failed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var locked = await _api.PostAsync("login", new { email, password = AccountsApi.Password }, _cancellationToken);

        locked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await DetailAsync(locked)).ShouldBe(LoginHandler.FailedMessage);
    }

    [Fact]
    public async Task Registering_a_confirmed_address_changes_nothing_and_tells_the_owner()
    {
        var email = AccountsApi.NewEmail();
        await _api.CreateConfirmedAccountAsync(email, _cancellationToken);

        await _api.RegisterAsync(email, "a different password", _cancellationToken);

        (await _api.LastEmailToAsync(email, _cancellationToken)).Subject.ShouldBe("You already have an account");
        await _api.LoginAsync(email, AccountsApi.Password, tenantId: null, _cancellationToken);
        using var newPassword = await _api.PostAsync("login", new { email, password = "a different password" }, _cancellationToken);
        newPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Only_the_latest_registration_of_an_unconfirmed_address_can_be_confirmed()
    {
        var email = AccountsApi.NewEmail();
        await _api.RegisterAsync(email, "first registration password", _cancellationToken);
        var firstLink = AccountsApi.TokenFrom(await _api.LastEmailToAsync(email, _cancellationToken));
        await _api.RegisterAsync(email, AccountsApi.Password, _cancellationToken);
        var secondLink = AccountsApi.TokenFrom(await _api.LastEmailToAsync(email, _cancellationToken));

        using var first = await _api.PostAsync("confirm-email", new { token = firstLink }, _cancellationToken);
        using var second = await _api.PostAsync("confirm-email", new { token = secondLink }, _cancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await DetailAsync(first)).ShouldBe(OneTimeToken.InvalidMessage);
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _api.LoginAsync(email, AccountsApi.Password, tenantId: null, _cancellationToken);
        using var firstPassword = await _api.PostAsync(
            "login", new { email, password = "first registration password" }, _cancellationToken);
        firstPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Confirmation_link_works_once()
    {
        var email = AccountsApi.NewEmail();
        await _api.RegisterAsync(email, AccountsApi.Password, _cancellationToken);
        var token = AccountsApi.TokenFrom(await _api.LastEmailToAsync(email, _cancellationToken));

        using var first = await _api.PostAsync("confirm-email", new { token }, _cancellationToken);
        using var again = await _api.PostAsync("confirm-email", new { token }, _cancellationToken);
        using var unknown = await _api.PostAsync("confirm-email", new { token = "not-a-token" }, _cancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Short_password_and_invalid_address_are_rejected_before_anything_is_stored()
    {
        var email = AccountsApi.NewEmail();

        using var shortPassword = await _api.PostAsync("register", new { email, password = "too short" }, _cancellationToken);
        using var invalidAddress = await _api.PostAsync(
            "register", new { email = "not an address", password = AccountsApi.Password }, _cancellationToken);

        shortPassword.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        invalidAddress.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _api.EmailsToAsync(email, _cancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sign_in_for_a_tenant_requires_a_role_in_it_and_puts_the_tenant_in_the_token()
    {
        var email = AccountsApi.NewEmail();
        var userId = await _api.CreateConfirmedAccountAsync(email, _cancellationToken);
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        var otherTenantId = await _data.CreateTenantAsync(_cancellationToken);
        await _data.GrantAsync(tenantId, userId, [RolePermissions.Read], _cancellationToken);

        using var notMember = await _api.PostAsync(
            "login", new { email, password = AccountsApi.Password, tenantId = otherTenantId }, _cancellationToken);
        var tokens = await _api.LoginAsync(email, AccountsApi.Password, tenantId, _cancellationToken);

        notMember.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _api.MeAsync(tokens.AccessToken, _cancellationToken)).TenantId.ShouldBe(tenantId);

        // The token works with permission checks in that tenant (ADR-0017).
        using var client = _api.Host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var roles = await client.GetAsync("/api/v1/roles", _cancellationToken);
        roles.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Losing_the_tenant_membership_ends_the_session_at_the_next_refresh()
    {
        var email = AccountsApi.NewEmail();
        var userId = await _api.CreateConfirmedAccountAsync(email, _cancellationToken);
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        var roleId = await _data.GrantAsync(tenantId, userId, [RolePermissions.Read], _cancellationToken);
        var tokens = await _api.LoginAsync(email, AccountsApi.Password, tenantId, _cancellationToken);

        await _data.UnassignAsync(tenantId, userId, roleId, _cancellationToken);
        using var refresh = await _api.PostAsync("refresh", new { refreshToken = tokens.RefreshToken }, _cancellationToken);

        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await DetailAsync(refresh)).ShouldBe(RefreshHandler.FailedMessage);
    }

    [Fact]
    public async Task Account_endpoints_do_not_exist_unless_enabled()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/account/login", new { email = "a@b.example", password = AccountsApi.Password }, _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Account_requests_are_limited_per_client_ip()
    {
        await using var limited = AccountsApi.Start(factory, new Dictionary<string, string?>
        {
            ["RateLimiting:AccountsPermitLimit"] = "2",
            ["RateLimiting:AccountsWindow"] = "00:01:00",
        });
        var body = new { email = AccountsApi.NewEmail() };

        using var first = await limited.PostAsync("forgot-password", body, _cancellationToken);
        using var second = await limited.PostAsync("forgot-password", body, _cancellationToken);
        using var third = await limited.PostAsync("forgot-password", body, _cancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        second.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    public async ValueTask DisposeAsync()
    {
        await _api.DisposeAsync();
        await _data.DisposeAsync();
    }

    private async Task<string?> DetailAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(_cancellationToken)).GetProperty("detail").GetString();
}
