using System.Net;
using System.Net.Http.Json;
using Boilerplate.Application.Common.Auditing;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Features.Auditing;
using Boilerplate.Application.Features.Invitations;
using Boilerplate.Application.Features.Invitations.CreateInvitation;
using Boilerplate.Application.Features.Projects;
using Boilerplate.Application.Features.Roles;
using Boilerplate.Application.Features.Tenants.CreateTenant;
using Boilerplate.Application.Features.Tenants.RenameTenant;
using Boilerplate.IntegrationTests.Platform;
using Boilerplate.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Boilerplate.IntegrationTests.Auditing;

/// <summary>Recording and reading the audit log over HTTP (ADR-0028).</summary>
public sealed class AuditLogTests(ApiFactory factory, PostgresFixture postgres) : IClassFixture<ApiFactory>, IAsyncDisposable
{
    private const string AuditLog = "/api/v1/audit-log";
    private const string Roles = "/api/v1/roles";
    private const string Invitations = "/api/v1/invitations";

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly AuthorizationData _data = new(postgres);
    private readonly WebApplicationFactory<Program> _api = factory.WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Invitations:AcceptUrl"] = PlatformTenantsTests.AcceptUrl })));

    [Fact]
    public async Task Role_changes_are_recorded_newest_first_with_the_actor()
    {
        var tenant = await NewTenantAsync();
        var memberId = $"member-{Guid.NewGuid():N}";

        var role = await CreateRoleAsync(tenant.Admin, "Editors", [ProjectPermissions.Read]);
        await SucceedAsync(tenant.Admin.PutAsJsonAsync(
            $"{Roles}/{role.Id}", new SaveRoleRequest("Writers", [ProjectPermissions.Manage]), _cancellationToken));
        await SucceedAsync(tenant.Admin.PutAsync($"{Roles}/{role.Id}/assignments/{memberId}", content: null, _cancellationToken));
        await SucceedAsync(tenant.Admin.DeleteAsync($"{Roles}/{role.Id}/assignments/{memberId}", _cancellationToken));
        await SucceedAsync(tenant.Admin.DeleteAsync($"{Roles}/{role.Id}", _cancellationToken));

        var entries = await EntriesAsync(tenant.Admin, $"?targetId={role.Id}");

        entries.Select(entry => entry.Action).ShouldBe(
        [
            AuditActions.RoleDeleted,
            AuditActions.RoleUnassigned,
            AuditActions.RoleAssigned,
            AuditActions.RoleUpdated,
            AuditActions.RoleCreated,
        ]);
        entries.ShouldAllBe(entry => entry.ActorId == tenant.AdminUserId);
        entries[4].Description.ShouldBe($"Created role 'Editors' with permissions: {ProjectPermissions.Read}.");
        entries[3].Description.ShouldBe(
            $"Changed role 'Writers': renamed from 'Editors'; added {ProjectPermissions.Manage}; removed {ProjectPermissions.Read}.");
        entries[2].Description.ShouldContain(memberId);
    }

    [Fact]
    public async Task Requests_that_change_nothing_record_nothing()
    {
        var tenant = await NewTenantAsync();
        var memberId = $"member-{Guid.NewGuid():N}";
        var role = await CreateRoleAsync(tenant.Admin, "Readers", [ProjectPermissions.Read]);
        await SucceedAsync(tenant.Admin.PutAsync($"{Roles}/{role.Id}/assignments/{memberId}", content: null, _cancellationToken));

        await SucceedAsync(tenant.Admin.PutAsJsonAsync(
            $"{Roles}/{role.Id}", new SaveRoleRequest("Readers", [ProjectPermissions.Read]), _cancellationToken));
        await SucceedAsync(tenant.Admin.PutAsync($"{Roles}/{role.Id}/assignments/{memberId}", content: null, _cancellationToken));

        (await EntriesAsync(tenant.Admin, $"?targetId={role.Id}")).Select(entry => entry.Action)
            .ShouldBe([AuditActions.RoleAssigned, AuditActions.RoleCreated]);
    }

    [Fact]
    public async Task Failed_requests_record_nothing()
    {
        var tenant = await NewTenantAsync();
        await CreateRoleAsync(tenant.Admin, "Editors", []);

        using var duplicate = await tenant.Admin.PostAsJsonAsync(Roles, new SaveRoleRequest("Editors", []), _cancellationToken);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await EntriesAsync(tenant.Admin, $"?action={AuditActions.RoleCreated}")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Invitations_are_recorded_without_the_address()
    {
        var tenant = await NewTenantAsync();
        var email = $"{Guid.NewGuid():N}@tests.example";
        var inviteeId = $"invitee-{Guid.NewGuid():N}";

        using var created = await tenant.Admin.PostAsJsonAsync(
            Invitations, new CreateInvitationRequest(email, tenant.AdministratorRoleId), _cancellationToken);
        var invitation = (await created.Content.ReadFromJsonAsync<InvitationResponse>(_cancellationToken))!;
        var token = QueuedEmails.TokenFrom((await QueuedEmails.ToAsync(_api.Services, email, _cancellationToken)).Single());
        using var invitee = _api.CreateAuthenticatedClient(TestTokens.Create(inviteeId));
        await SucceedAsync(invitee.PostAsJsonAsync($"{Invitations}/accept", new { token }, _cancellationToken));

        var entries = await EntriesAsync(tenant.Admin, $"?targetId={invitation.Id}");

        entries.Select(entry => (entry.Action, entry.ActorId)).ShouldBe(
        [
            (AuditActions.InvitationAccepted, inviteeId),
            (AuditActions.InvitationCreated, tenant.AdminUserId),
        ]);
        entries.ShouldAllBe(entry => !entry.Description.Contains(email, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Revoking_a_pending_invitation_is_recorded_once()
    {
        var tenant = await NewTenantAsync();
        using var created = await tenant.Admin.PostAsJsonAsync(
            Invitations, new CreateInvitationRequest($"{Guid.NewGuid():N}@tests.example", tenant.AdministratorRoleId), _cancellationToken);
        var invitation = (await created.Content.ReadFromJsonAsync<InvitationResponse>(_cancellationToken))!;

        await SucceedAsync(tenant.Admin.DeleteAsync($"{Invitations}/{invitation.Id}", _cancellationToken));
        await SucceedAsync(tenant.Admin.DeleteAsync($"{Invitations}/{invitation.Id}", _cancellationToken));

        (await EntriesAsync(tenant.Admin, $"?targetId={invitation.Id}")).Select(entry => entry.Action)
            .ShouldBe([AuditActions.InvitationRevoked, AuditActions.InvitationCreated]);
    }

    [Fact]
    public async Task Platform_administration_is_recorded_in_the_affected_tenant()
    {
        var platformAdminId = $"platform-{Guid.NewGuid():N}";
        await _data.SetPlatformAdministratorAsync(platformAdminId, isAdministrator: true, _cancellationToken);
        using var platformAdmin = _api.CreateAuthenticatedClient(TestTokens.Create(platformAdminId));
        var name = $"audit-{Guid.NewGuid():N}";

        using var response = await platformAdmin.PostAsJsonAsync(
            "/api/v1/platform/tenants", new CreateTenantRequest(name, $"{Guid.NewGuid():N}@tests.example"), _cancellationToken);
        var created = (await response.Content.ReadFromJsonAsync<CreateTenantResponse>(_cancellationToken))!;
        await SucceedAsync(platformAdmin.PutAsJsonAsync(
            $"/api/v1/platform/tenants/{created.Tenant.Id}", new RenameTenantRequest($"{name}-renamed"), _cancellationToken));

        var readerId = $"auditor-{Guid.NewGuid():N}";
        await _data.GrantAsync(created.Tenant.Id, readerId, [AuditPermissions.Read], _cancellationToken);
        using var reader = _api.CreateAuthenticatedClient(TestTokens.Create(readerId, created.Tenant.Id));
        var entries = await EntriesAsync(reader, $"?actorId={platformAdminId}");

        entries.Select(entry => entry.Action).ShouldBe(
            [AuditActions.TenantRenamed, AuditActions.InvitationCreated, AuditActions.RoleCreated, AuditActions.TenantCreated],
            ignoreOrder: true);
        entries.Single(entry => entry.Action == AuditActions.TenantRenamed).Description
            .ShouldBe($"Renamed tenant from '{name}' to '{name}-renamed'.");
    }

    [Fact]
    public async Task Filters_by_action_and_actor()
    {
        var tenant = await NewTenantAsync();
        var managerId = $"manager-{Guid.NewGuid():N}";
        await _data.GrantAsync(tenant.Id, managerId, [RolePermissions.Manage, ProjectPermissions.Read], _cancellationToken);
        using var manager = _api.CreateAuthenticatedClient(TestTokens.Create(managerId, tenant.Id));

        var byAdmin = await CreateRoleAsync(tenant.Admin, "By admin", []);
        var byManager = await CreateRoleAsync(manager, "By manager", [ProjectPermissions.Read]);
        await SucceedAsync(tenant.Admin.DeleteAsync($"{Roles}/{byAdmin.Id}", _cancellationToken));

        (await EntriesAsync(tenant.Admin, $"?actorId={managerId}")).Select(entry => entry.TargetId).ShouldBe([byManager.Id]);
        (await EntriesAsync(tenant.Admin, $"?action={AuditActions.RoleDeleted}")).Select(entry => entry.TargetId).ShouldBe([byAdmin.Id]);
    }

    [Fact]
    public async Task Pages_are_bounded()
    {
        var tenant = await NewTenantAsync();
        await CreateRoleAsync(tenant.Admin, "One", []);
        await CreateRoleAsync(tenant.Admin, "Two", []);

        var page = await tenant.Admin.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>($"{AuditLog}?pageSize=1", _cancellationToken);

        page!.Items.Count.ShouldBe(1);
        page.TotalCount.ShouldBe(2);
        (await StatusAsync(tenant.Admin.GetAsync($"{AuditLog}?pageSize=0", _cancellationToken))).ShouldBe(HttpStatusCode.BadRequest);
        (await StatusAsync(tenant.Admin.GetAsync($"{AuditLog}?action={new string('a', 65)}", _cancellationToken)))
            .ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reading_needs_the_audit_permission_in_the_callers_tenant()
    {
        var tenantA = await NewTenantAsync();
        var tenantB = await NewTenantAsync();
        var userId = $"user-{Guid.NewGuid():N}";
        await _data.GrantAsync(tenantA.Id, userId, [RolePermissions.Read], _cancellationToken);
        await _data.GrantAsync(tenantB.Id, userId, [AuditPermissions.Read], _cancellationToken);
        using var withoutPermission = _api.CreateAuthenticatedClient(TestTokens.Create(userId, tenantA.Id));
        using var withPermission = _api.CreateAuthenticatedClient(TestTokens.Create(userId, tenantB.Id));
        using var anonymous = _api.CreateClient();

        (await StatusAsync(anonymous.GetAsync(AuditLog, _cancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(withoutPermission.GetAsync(AuditLog, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(withPermission.GetAsync(AuditLog, _cancellationToken))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Entries_of_other_tenants_are_not_listed()
    {
        var tenantA = await NewTenantAsync();
        var tenantB = await NewTenantAsync();
        var roleOfA = await CreateRoleAsync(tenantA.Admin, "Editors", []);

        (await EntriesAsync(tenantB.Admin, $"?targetId={roleOfA.Id}")).ShouldBeEmpty();
        (await EntriesAsync(tenantB.Admin, $"?actorId={tenantA.AdminUserId}")).ShouldBeEmpty();
    }

    public async ValueTask DisposeAsync()
    {
        await _api.DisposeAsync();
        await _data.DisposeAsync();
    }

    private async Task<TenantWithAdministrator> NewTenantAsync()
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        var adminUserId = $"admin-{Guid.NewGuid():N}";
        var administratorRoleId = await _data.MakeAdministratorAsync(tenantId, adminUserId, _cancellationToken);
        var admin = _api.CreateAuthenticatedClient(TestTokens.Create(adminUserId, tenantId));
        return new TenantWithAdministrator(tenantId, adminUserId, administratorRoleId, admin);
    }

    private async Task<RoleResponse> CreateRoleAsync(HttpClient client, string name, string[] permissions)
    {
        using var response = await client.PostAsJsonAsync(Roles, new SaveRoleRequest(name, permissions), _cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(_cancellationToken));
        return (await response.Content.ReadFromJsonAsync<RoleResponse>(_cancellationToken))!;
    }

    private async Task<IReadOnlyList<AuditLogEntryResponse>> EntriesAsync(HttpClient client, string query)
    {
        var page = await client.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>($"{AuditLog}{query}", _cancellationToken);
        return page!.Items;
    }

    private async Task SucceedAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync(_cancellationToken));
    }

    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return response.StatusCode;
    }

    private sealed record TenantWithAdministrator(Guid Id, string AdminUserId, Guid AdministratorRoleId, HttpClient Admin);
}
