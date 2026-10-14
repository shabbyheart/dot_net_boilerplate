using System.Net;
using System.Net.Http.Json;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Features.Invitations;
using Boilerplate.Application.Features.Invitations.AcceptInvitation;
using Boilerplate.Application.Features.Tenants;
using Boilerplate.Application.Features.Tenants.CreateTenant;
using Boilerplate.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Boilerplate.IntegrationTests.Platform;

/// <summary>Platform administration of tenants and the administrator invitation (ADR-0027).</summary>
public sealed class PlatformTenantsTests(ApiFactory factory, PostgresFixture postgres)
    : IClassFixture<ApiFactory>, IAsyncDisposable
{
    public const string AcceptUrl = "https://app.example/accept-invitation";

    private const string Tenants = "/api/v1/platform/tenants";

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly AuthorizationData _data = new(postgres);
    private readonly WebApplicationFactory<Program> _api = factory.WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Invitations:AcceptUrl"] = AcceptUrl })));

    [Fact]
    public async Task Anonymous_callers_get_401()
    {
        using var client = _api.CreateClient();

        (await StatusAsync(client.GetAsync(Tenants, _cancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Tenant_administrators_who_are_not_platform_administrators_get_403()
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        var adminUserId = $"admin-{Guid.NewGuid():N}";
        await _data.MakeAdministratorAsync(tenantId, adminUserId, _cancellationToken);
        using var tenantAdmin = _api.CreateAuthenticatedClient(TestTokens.Create(adminUserId, tenantId));

        (await StatusAsync(tenantAdmin.GetAsync(Tenants, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(tenantAdmin.PostAsJsonAsync(Tenants, new CreateTenantRequest(NewName(), "a@example.com"), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(tenantAdmin.PutAsJsonAsync($"{Tenants}/{tenantId}", new { name = NewName() }, _cancellationToken)))
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Created_tenant_gets_its_first_administrator_through_the_invitation()
    {
        using var platformAdmin = await PlatformAdministratorAsync();
        var name = NewName();
        var email = $"{Guid.NewGuid():N}@tests.example";

        using var response = await platformAdmin.PostAsJsonAsync(Tenants, new CreateTenantRequest($"  {name} ", email), _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<CreateTenantResponse>(_cancellationToken))!;
        created.Tenant.Name.ShouldBe(name);
        response.Headers.Location!.AbsolutePath.ShouldBe($"{Tenants}/{created.Tenant.Id}");
        created.AdministratorInvitation.Email.ShouldBe(email);
        var (administratorRoles, administrators) = await _data.AdministratorsAsync(created.Tenant.Id, _cancellationToken);
        administratorRoles.ShouldBe(1);
        administrators.ShouldBeEmpty();

        var invitationEmail = (await QueuedEmails.ToAsync(_api.Services, email, _cancellationToken)).Single();
        invitationEmail.Subject.ShouldBe($"You are invited to {name}");
        invitationEmail.TextBody.ShouldContain(AcceptUrl + "?token=");

        // Whoever signs in with the link becomes the administrator; their token names no tenant yet.
        var newAdmin = $"new-admin-{Guid.NewGuid():N}";
        using var invitee = _api.CreateAuthenticatedClient(TestTokens.Create(newAdmin));
        using var accepted = await invitee.PostAsJsonAsync(
            "/api/v1/invitations/accept", new { token = QueuedEmails.TokenFrom(invitationEmail) }, _cancellationToken);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await accepted.Content.ReadFromJsonAsync<AcceptInvitationResponse>(_cancellationToken))!.TenantId.ShouldBe(created.Tenant.Id);
        (await _data.AdministratorsAsync(created.Tenant.Id, _cancellationToken)).AdministratorUserIds.ShouldBe([newAdmin]);

        using var asAdministrator = _api.CreateAuthenticatedClient(TestTokens.Create(newAdmin, created.Tenant.Id));
        (await StatusAsync(asAdministrator.GetAsync("/api/v1/roles", _cancellationToken))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Platform_administrators_get_no_access_inside_tenants()
    {
        var platformAdminId = $"platform-{Guid.NewGuid():N}";
        await _data.SetPlatformAdministratorAsync(platformAdminId, isAdministrator: true, _cancellationToken);
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        using var client = _api.CreateAuthenticatedClient(TestTokens.Create(platformAdminId, tenantId));

        (await StatusAsync(client.GetAsync("/api/v1/roles", _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(client.GetAsync(Tenants, _cancellationToken))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Lists_gets_and_renames_tenants()
    {
        using var platformAdmin = await PlatformAdministratorAsync();
        var tenant = await CreateTenantAsync(platformAdmin, NewName());
        var newName = NewName();

        using var renamed = await platformAdmin.PutAsJsonAsync($"{Tenants}/{tenant.Id}", new { name = newName }, _cancellationToken);
        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await platformAdmin.GetFromJsonAsync<TenantResponse>($"{Tenants}/{tenant.Id}", _cancellationToken))!.Name.ShouldBe(newName);
        var page = await platformAdmin.GetFromJsonAsync<PagedResponse<TenantResponse>>($"{Tenants}?pageSize=100", _cancellationToken);
        page!.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
        page.Items.Select(t => t.Name).ShouldBe(page.Items.Select(t => t.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Duplicate_names_return_409_and_unknown_tenants_404()
    {
        using var platformAdmin = await PlatformAdministratorAsync();
        var first = await CreateTenantAsync(platformAdmin, NewName());
        var second = await CreateTenantAsync(platformAdmin, NewName());

        (await StatusAsync(platformAdmin.PostAsJsonAsync(Tenants, new CreateTenantRequest(first.Name, "a@example.com"), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Conflict);
        (await StatusAsync(platformAdmin.PutAsJsonAsync($"{Tenants}/{second.Id}", new { name = first.Name }, _cancellationToken)))
            .ShouldBe(HttpStatusCode.Conflict);
        (await StatusAsync(platformAdmin.GetAsync($"{Tenants}/{Guid.NewGuid()}", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await StatusAsync(platformAdmin.PutAsJsonAsync($"{Tenants}/{Guid.NewGuid()}", new { name = NewName() }, _cancellationToken)))
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_requests_return_400()
    {
        using var platformAdmin = await PlatformAdministratorAsync();

        (await StatusAsync(platformAdmin.PostAsJsonAsync(Tenants, new CreateTenantRequest(NewName(), "not-an-address"), _cancellationToken)))
            .ShouldBe(HttpStatusCode.BadRequest);
        (await StatusAsync(platformAdmin.PostAsJsonAsync(Tenants, new CreateTenantRequest(" ", "a@example.com"), _cancellationToken)))
            .ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Inviting_another_administrator_replaces_the_pending_invitation()
    {
        using var platformAdmin = await PlatformAdministratorAsync();
        var email = $"{Guid.NewGuid():N}@tests.example";
        using var created = await platformAdmin.PostAsJsonAsync(Tenants, new CreateTenantRequest(NewName(), email), _cancellationToken);
        var tenantId = (await created.Content.ReadFromJsonAsync<CreateTenantResponse>(_cancellationToken))!.Tenant.Id;

        using var reinvited = await platformAdmin.PostAsJsonAsync(
            $"{Tenants}/{tenantId}/administrator-invitations", new { email }, _cancellationToken);

        reinvited.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await reinvited.Content.ReadFromJsonAsync<InvitationResponse>(_cancellationToken))!.Email.ShouldBe(email);
        var emails = await QueuedEmails.ToAsync(_api.Services, email, _cancellationToken);
        emails.Count.ShouldBe(2);

        using var invitee = _api.CreateAuthenticatedClient(TestTokens.Create($"user-{Guid.NewGuid():N}"));
        (await StatusAsync(invitee.PostAsJsonAsync("/api/v1/invitations/accept", new { token = QueuedEmails.TokenFrom(emails[0]) }, _cancellationToken)))
            .ShouldBe(HttpStatusCode.BadRequest);
        (await StatusAsync(invitee.PostAsJsonAsync("/api/v1/invitations/accept", new { token = QueuedEmails.TokenFrom(emails[1]) }, _cancellationToken)))
            .ShouldBe(HttpStatusCode.OK);

        (await StatusAsync(platformAdmin.PostAsJsonAsync(
            $"{Tenants}/{Guid.NewGuid()}/administrator-invitations", new { email }, _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Without_an_accept_page_creating_a_tenant_returns_409_and_creates_nothing()
    {
        var platformAdminId = $"platform-{Guid.NewGuid():N}";
        await _data.SetPlatformAdministratorAsync(platformAdminId, isAdministrator: true, _cancellationToken);
        using var platformAdmin = factory.CreateAuthenticatedClient(TestTokens.Create(platformAdminId));
        var name = NewName();

        (await StatusAsync(platformAdmin.PostAsJsonAsync(Tenants, new CreateTenantRequest(name, "a@example.com"), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Conflict);

        (await _data.FindTenantAsync(name, _cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Revoking_platform_administration_is_effective_on_the_next_request()
    {
        var platformAdminId = $"platform-{Guid.NewGuid():N}";
        await _data.SetPlatformAdministratorAsync(platformAdminId, isAdministrator: true, _cancellationToken);
        using var client = _api.CreateAuthenticatedClient(TestTokens.Create(platformAdminId));
        (await StatusAsync(client.GetAsync(Tenants, _cancellationToken))).ShouldBe(HttpStatusCode.OK);

        await _data.SetPlatformAdministratorAsync(platformAdminId, isAdministrator: false, _cancellationToken);

        (await StatusAsync(client.GetAsync(Tenants, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
    }

    public async ValueTask DisposeAsync()
    {
        await _api.DisposeAsync();
        await _data.DisposeAsync();
    }

    private static string NewName() => $"tenant-{Guid.NewGuid():N}";

    private async Task<HttpClient> PlatformAdministratorAsync()
    {
        var userId = $"platform-{Guid.NewGuid():N}";
        await _data.SetPlatformAdministratorAsync(userId, isAdministrator: true, _cancellationToken);

        // A platform administrator's token need not name a tenant.
        return _api.CreateAuthenticatedClient(TestTokens.Create(userId));
    }

    private async Task<TenantResponse> CreateTenantAsync(HttpClient platformAdmin, string name)
    {
        using var response = await platformAdmin.PostAsJsonAsync(
            Tenants, new CreateTenantRequest(name, $"{Guid.NewGuid():N}@tests.example"), _cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreateTenantResponse>(_cancellationToken))!.Tenant;
    }

    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return response.StatusCode;
    }
}
