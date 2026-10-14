using System.Net;
using System.Net.Http.Json;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Features.Invitations;
using Boilerplate.Application.Features.Invitations.AcceptInvitation;
using Boilerplate.Application.Features.Invitations.CreateInvitation;
using Boilerplate.Application.Features.Projects;
using Boilerplate.Application.Features.Roles;
using Boilerplate.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Boilerplate.IntegrationTests.Platform;

/// <summary>Invitations to tenant roles: issuing, the escalation guard, acceptance and revocation (ADR-0027).</summary>
public sealed class InvitationsTests(ApiFactory factory, PostgresFixture postgres) : IClassFixture<ApiFactory>, IAsyncDisposable
{
    private const string Invitations = "/api/v1/invitations";
    private const string Accept = "/api/v1/invitations/accept";

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly AuthorizationData _data = new(postgres);
    private readonly WebApplicationFactory<Program> _api = factory.WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Invitations:AcceptUrl"] = PlatformTenantsTests.AcceptUrl })));

    [Fact]
    public async Task Invited_user_receives_the_role_once()
    {
        var tenant = await NewTenantAsync();
        var readers = await _data.GrantAsync(tenant.Id, $"nobody-{Guid.NewGuid():N}", [ProjectPermissions.Read], _cancellationToken);
        var email = NewEmail();

        using var created = await tenant.Admin.PostAsJsonAsync(Invitations, new CreateInvitationRequest(email.ToUpperInvariant(), readers), _cancellationToken);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var invitation = (await created.Content.ReadFromJsonAsync<InvitationResponse>(_cancellationToken))!;
        invitation.Email.ShouldBe(email);
        created.Headers.Location!.AbsolutePath.ShouldBe($"{Invitations}/{invitation.Id}");
        var token = QueuedEmails.TokenFrom((await QueuedEmails.ToAsync(_api.Services, email, _cancellationToken)).Single());

        var inviteeId = $"invitee-{Guid.NewGuid():N}";
        using var invitee = _api.CreateAuthenticatedClient(TestTokens.Create(inviteeId));
        using var accepted = await invitee.PostAsJsonAsync(Accept, new { token }, _cancellationToken);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await accepted.Content.ReadFromJsonAsync<AcceptInvitationResponse>(_cancellationToken))
            .ShouldBe(new AcceptInvitationResponse(tenant.Id, readers));
        (await _data.AssigneesAsync(tenant.Id, readers, _cancellationToken)).ShouldContain(inviteeId);

        using var member = _api.CreateAuthenticatedClient(TestTokens.Create(inviteeId, tenant.Id));
        (await StatusAsync(member.GetAsync("/api/v1/projects", _cancellationToken))).ShouldBe(HttpStatusCode.OK);

        var stored = await tenant.Admin.GetFromJsonAsync<InvitationResponse>($"{Invitations}/{invitation.Id}", _cancellationToken);
        stored!.AcceptedBy.ShouldBe(inviteeId);
        stored.AcceptedAt.ShouldNotBeNull();

        // Single use, also for someone else holding the link.
        using var other = _api.CreateAuthenticatedClient(TestTokens.Create($"other-{Guid.NewGuid():N}"));
        (await StatusAsync(other.PostAsJsonAsync(Accept, new { token }, _cancellationToken))).ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lists_the_tenants_invitations_newest_first()
    {
        var tenant = await NewTenantAsync();
        var first = await InviteAsync(tenant.Admin, NewEmail(), tenant.AdministratorRoleId);
        var second = await InviteAsync(tenant.Admin, NewEmail(), tenant.AdministratorRoleId);

        var page = await tenant.Admin.GetFromJsonAsync<PagedResponse<InvitationResponse>>(Invitations, _cancellationToken);

        page!.Items.Select(invitation => invitation.Id).ShouldBe([second.Id, first.Id]);
    }

    [Fact]
    public async Task Revoked_invitation_cannot_be_accepted()
    {
        var tenant = await NewTenantAsync();
        var email = NewEmail();
        var invitation = await InviteAsync(tenant.Admin, email, tenant.AdministratorRoleId);
        var token = QueuedEmails.TokenFrom((await QueuedEmails.ToAsync(_api.Services, email, _cancellationToken)).Single());

        (await StatusAsync(tenant.Admin.DeleteAsync($"{Invitations}/{invitation.Id}", _cancellationToken))).ShouldBe(HttpStatusCode.NoContent);

        using var invitee = _api.CreateAuthenticatedClient(TestTokens.Create($"invitee-{Guid.NewGuid():N}"));
        (await StatusAsync(invitee.PostAsJsonAsync(Accept, new { token }, _cancellationToken))).ShouldBe(HttpStatusCode.BadRequest);
        (await tenant.Admin.GetFromJsonAsync<InvitationResponse>($"{Invitations}/{invitation.Id}", _cancellationToken))!
            .RevokedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Inviting_is_limited_to_roles_the_caller_could_assign()
    {
        var tenant = await NewTenantAsync();
        var editors = await _data.GrantAsync(tenant.Id, $"nobody-{Guid.NewGuid():N}", [ProjectPermissions.Manage], _cancellationToken);
        var managerId = $"manager-{Guid.NewGuid():N}";
        await _data.GrantAsync(tenant.Id, managerId, [RolePermissions.Manage], _cancellationToken);
        using var manager = _api.CreateAuthenticatedClient(TestTokens.Create(managerId, tenant.Id));

        (await StatusAsync(manager.PostAsJsonAsync(Invitations, new CreateInvitationRequest(NewEmail(), editors), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(manager.PostAsJsonAsync(Invitations, new CreateInvitationRequest(NewEmail(), tenant.AdministratorRoleId), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Endpoints_need_role_permissions()
    {
        var tenant = await NewTenantAsync();
        var userId = $"reader-{Guid.NewGuid():N}";
        await _data.GrantAsync(tenant.Id, userId, [ProjectPermissions.Read], _cancellationToken);
        using var reader = _api.CreateAuthenticatedClient(TestTokens.Create(userId, tenant.Id));
        using var anonymous = _api.CreateClient();

        (await StatusAsync(reader.GetAsync(Invitations, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(reader.PostAsJsonAsync(Invitations, new CreateInvitationRequest(NewEmail(), tenant.AdministratorRoleId), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(anonymous.PostAsJsonAsync(Accept, new { token = "anything" }, _cancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invitations_of_other_tenants_are_not_found()
    {
        var tenantA = await NewTenantAsync();
        var tenantB = await NewTenantAsync();
        var invitationOfA = await InviteAsync(tenantA.Admin, NewEmail(), tenantA.AdministratorRoleId);

        (await StatusAsync(tenantB.Admin.GetAsync($"{Invitations}/{invitationOfA.Id}", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await StatusAsync(tenantB.Admin.DeleteAsync($"{Invitations}/{invitationOfA.Id}", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await StatusAsync(tenantB.Admin.PostAsJsonAsync(Invitations, new CreateInvitationRequest(NewEmail(), tenantA.AdministratorRoleId), _cancellationToken)))
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_tokens_and_requests_return_400()
    {
        var tenant = await NewTenantAsync();
        using var user = _api.CreateAuthenticatedClient(TestTokens.Create($"user-{Guid.NewGuid():N}"));

        (await StatusAsync(user.PostAsJsonAsync(Accept, new { token = "not-a-real-token" }, _cancellationToken))).ShouldBe(HttpStatusCode.BadRequest);
        (await StatusAsync(user.PostAsJsonAsync(Accept, new { token = "" }, _cancellationToken))).ShouldBe(HttpStatusCode.BadRequest);
        (await StatusAsync(tenant.Admin.PostAsJsonAsync(Invitations, new CreateInvitationRequest("nope", tenant.AdministratorRoleId), _cancellationToken)))
            .ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Accepting_a_role_already_held_keeps_one_assignment()
    {
        var tenant = await NewTenantAsync();
        var email = NewEmail();
        await InviteAsync(tenant.Admin, email, tenant.AdministratorRoleId);
        var token = QueuedEmails.TokenFrom((await QueuedEmails.ToAsync(_api.Services, email, _cancellationToken)).Single());

        (await StatusAsync(tenant.Admin.PostAsJsonAsync(Accept, new { token }, _cancellationToken))).ShouldBe(HttpStatusCode.OK);

        (await _data.AssigneesAsync(tenant.Id, tenant.AdministratorRoleId, _cancellationToken)).ShouldBe([tenant.AdminUserId]);
    }

    public async ValueTask DisposeAsync()
    {
        await _api.DisposeAsync();
        await _data.DisposeAsync();
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@tests.example";

    private async Task<TenantWithAdministrator> NewTenantAsync()
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        var adminUserId = $"admin-{Guid.NewGuid():N}";
        var administratorRoleId = await _data.MakeAdministratorAsync(tenantId, adminUserId, _cancellationToken);
        var admin = _api.CreateAuthenticatedClient(TestTokens.Create(adminUserId, tenantId));
        return new TenantWithAdministrator(tenantId, adminUserId, administratorRoleId, admin);
    }

    private async Task<InvitationResponse> InviteAsync(HttpClient client, string email, Guid roleId)
    {
        using var response = await client.PostAsJsonAsync(Invitations, new CreateInvitationRequest(email, roleId), _cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(_cancellationToken));
        return (await response.Content.ReadFromJsonAsync<InvitationResponse>(_cancellationToken))!;
    }

    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return response.StatusCode;
    }

    private sealed record TenantWithAdministrator(Guid Id, string AdminUserId, Guid AdministratorRoleId, HttpClient Admin);
}
