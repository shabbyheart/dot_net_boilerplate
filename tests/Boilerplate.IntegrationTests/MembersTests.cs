using System.Net;
using System.Net.Http.Json;
using Boilerplate.Application.Common.Auditing;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Features.Auditing;
using Boilerplate.Application.Features.Members;
using Boilerplate.Application.Features.Projects;
using Boilerplate.Application.Features.Roles;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests;

/// <summary>Tenant members: listing, removal and leaving (ADR-0029).</summary>
public sealed class MembersTests(ApiFactory factory, PostgresFixture postgres) : IClassFixture<ApiFactory>, IAsyncDisposable
{
    private const string Members = "/api/v1/members";
    private const string Membership = "/api/v1/me/membership";
    private const string Projects = "/api/v1/projects";

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly AuthorizationData _data = new(postgres);

    [Fact]
    public async Task Lists_members_with_their_roles_ordered_by_user_id()
    {
        var tenant = await NewTenantAsync();
        var readers = await _data.GrantAsync(tenant.Id, "user-b", [ProjectPermissions.Read], _cancellationToken);
        var editors = await _data.GrantAsync(tenant.Id, "user-b", [ProjectPermissions.Manage], _cancellationToken);
        await _data.GrantAsync(tenant.Id, "user-a", [ProjectPermissions.Read], _cancellationToken);

        var page = await tenant.Admin.GetFromJsonAsync<PagedResponse<MemberResponse>>(Members, _cancellationToken);

        page!.TotalCount.ShouldBe(3);
        page.Items.Select(member => member.UserId).ShouldBe([tenant.AdminUserId, "user-a", "user-b"]);
        page.Items[2].Roles.Select(role => role.Id).ShouldBe([readers, editors], ignoreOrder: true);
        page.Items[0].Roles.ShouldHaveSingleItem().IsAdministrator.ShouldBeTrue();

        var second = await tenant.Admin.GetFromJsonAsync<PagedResponse<MemberResponse>>($"{Members}?page=2&pageSize=2", _cancellationToken);
        second!.Items.Select(member => member.UserId).ShouldBe(["user-b"]);
    }

    [Fact]
    public async Task Gets_one_member_and_404_for_non_members()
    {
        var tenant = await NewTenantAsync();
        var other = await NewTenantAsync();
        await _data.GrantAsync(other.Id, "elsewhere", [ProjectPermissions.Read], _cancellationToken);

        var member = await tenant.Admin.GetFromJsonAsync<MemberResponse>($"{Members}/{tenant.AdminUserId}", _cancellationToken);

        member!.UserId.ShouldBe(tenant.AdminUserId);
        (await StatusAsync(tenant.Admin.GetAsync($"{Members}/nobody", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await StatusAsync(tenant.Admin.GetAsync($"{Members}/elsewhere", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Removing_a_member_takes_every_role_away_at_once()
    {
        var tenant = await NewTenantAsync();
        var memberId = $"member-{Guid.NewGuid():N}";
        var readers = await _data.GrantAsync(tenant.Id, memberId, [ProjectPermissions.Read], _cancellationToken);
        var editors = await _data.GrantAsync(tenant.Id, memberId, [ProjectPermissions.Manage], _cancellationToken);
        using var member = factory.CreateAuthenticatedClient(TestTokens.Create(memberId, tenant.Id));
        (await StatusAsync(member.GetAsync(Projects, _cancellationToken))).ShouldBe(HttpStatusCode.OK);

        (await StatusAsync(tenant.Admin.DeleteAsync($"{Members}/{memberId}", _cancellationToken))).ShouldBe(HttpStatusCode.NoContent);

        (await StatusAsync(member.GetAsync(Projects, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(tenant.Admin.GetAsync($"{Members}/{memberId}", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        var entries = await AuditEntriesAsync(tenant.Admin, AuditActions.RoleUnassigned);
        entries.Select(entry => entry.TargetId).ShouldBe([readers, editors], ignoreOrder: true);
        entries.ShouldAllBe(entry => entry.ActorId == tenant.AdminUserId && entry.Description.Contains(memberId));
    }

    [Fact]
    public async Task Removing_needs_every_role_of_the_member_to_be_one_the_caller_could_hand_out()
    {
        var tenant = await NewTenantAsync();
        var managerId = $"manager-{Guid.NewGuid():N}";
        await _data.GrantAsync(tenant.Id, managerId, [RolePermissions.Manage, ProjectPermissions.Read], _cancellationToken);
        using var manager = factory.CreateAuthenticatedClient(TestTokens.Create(managerId, tenant.Id));
        var memberId = $"member-{Guid.NewGuid():N}";
        await _data.GrantAsync(tenant.Id, memberId, [ProjectPermissions.Read], _cancellationToken);
        await _data.GrantAsync(tenant.Id, memberId, [ProjectPermissions.Manage], _cancellationToken);

        (await StatusAsync(manager.DeleteAsync($"{Members}/{memberId}", _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(manager.DeleteAsync($"{Members}/{tenant.AdminUserId}", _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);

        // Nothing was removed, not even the role the manager could have handed out.
        (await tenant.Admin.GetFromJsonAsync<MemberResponse>($"{Members}/{memberId}", _cancellationToken))!.Roles.Count.ShouldBe(2);
    }

    [Fact]
    public async Task The_last_administrator_can_be_neither_removed_nor_leave()
    {
        var tenant = await NewTenantAsync();

        (await StatusAsync(tenant.Admin.DeleteAsync($"{Members}/{tenant.AdminUserId}", _cancellationToken))).ShouldBe(HttpStatusCode.Conflict);
        (await StatusAsync(tenant.Admin.DeleteAsync(Membership, _cancellationToken))).ShouldBe(HttpStatusCode.Conflict);

        var secondAdminId = $"admin-{Guid.NewGuid():N}";
        await _data.AssignAsync(tenant.Id, secondAdminId, tenant.AdministratorRoleId, _cancellationToken);

        (await StatusAsync(tenant.Admin.DeleteAsync(Membership, _cancellationToken))).ShouldBe(HttpStatusCode.NoContent);
        (await _data.AssigneesAsync(tenant.Id, tenant.AdministratorRoleId, _cancellationToken)).ShouldBe([secondAdminId]);
    }

    [Fact]
    public async Task Members_can_leave_without_any_permission()
    {
        var tenant = await NewTenantAsync();
        var memberId = $"member-{Guid.NewGuid():N}";
        var readers = await _data.GrantAsync(tenant.Id, memberId, [ProjectPermissions.Read], _cancellationToken);
        using var member = factory.CreateAuthenticatedClient(TestTokens.Create(memberId, tenant.Id));

        (await StatusAsync(member.DeleteAsync(Membership, _cancellationToken))).ShouldBe(HttpStatusCode.NoContent);

        (await _data.AssigneesAsync(tenant.Id, readers, _cancellationToken)).ShouldBeEmpty();
        (await StatusAsync(member.DeleteAsync(Membership, _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        var entry = (await AuditEntriesAsync(tenant.Admin, AuditActions.RoleUnassigned)).ShouldHaveSingleItem();
        entry.ActorId.ShouldBe(memberId);
        entry.Description.ShouldContain("left the tenant");
    }

    [Fact]
    public async Task Leaving_without_a_tenant_in_the_token_finds_no_membership()
    {
        var tenant = await NewTenantAsync();
        using var noTenant = factory.CreateAuthenticatedClient(TestTokens.Create(tenant.AdminUserId));

        (await StatusAsync(noTenant.DeleteAsync(Membership, _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await _data.AssigneesAsync(tenant.Id, tenant.AdministratorRoleId, _cancellationToken)).ShouldBe([tenant.AdminUserId]);
    }

    [Fact]
    public async Task Endpoints_need_role_permissions_in_the_callers_tenant()
    {
        var tenant = await NewTenantAsync();
        var other = await NewTenantAsync();
        var userId = $"user-{Guid.NewGuid():N}";
        await _data.GrantAsync(tenant.Id, userId, [ProjectPermissions.Read], _cancellationToken);
        await _data.GrantAsync(other.Id, userId, [RolePermissions.Read, RolePermissions.Manage], _cancellationToken);
        using var withoutPermission = factory.CreateAuthenticatedClient(TestTokens.Create(userId, tenant.Id));
        var readerId = $"reader-{Guid.NewGuid():N}";
        await _data.GrantAsync(tenant.Id, readerId, [RolePermissions.Read], _cancellationToken);
        using var reader = factory.CreateAuthenticatedClient(TestTokens.Create(readerId, tenant.Id));
        using var anonymous = factory.CreateClient();

        (await StatusAsync(anonymous.GetAsync(Members, _cancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(anonymous.DeleteAsync($"{Members}/{userId}", _cancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(anonymous.DeleteAsync(Membership, _cancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(withoutPermission.GetAsync(Members, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(withoutPermission.DeleteAsync($"{Members}/{readerId}", _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(reader.GetAsync(Members, _cancellationToken))).ShouldBe(HttpStatusCode.OK);
        (await StatusAsync(reader.DeleteAsync($"{Members}/{userId}", _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Members_of_other_tenants_are_neither_listed_nor_removable()
    {
        var tenantA = await NewTenantAsync();
        var tenantB = await NewTenantAsync();
        var memberOfA = $"member-{Guid.NewGuid():N}";
        await _data.GrantAsync(tenantA.Id, memberOfA, [ProjectPermissions.Read], _cancellationToken);

        var page = await tenantB.Admin.GetFromJsonAsync<PagedResponse<MemberResponse>>(Members, _cancellationToken);

        page!.Items.Select(member => member.UserId).ShouldBe([tenantB.AdminUserId]);
        (await StatusAsync(tenantB.Admin.DeleteAsync($"{Members}/{memberOfA}", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await tenantA.Admin.GetFromJsonAsync<MemberResponse>($"{Members}/{memberOfA}", _cancellationToken)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Invalid_paging_returns_400()
    {
        var tenant = await NewTenantAsync();

        (await StatusAsync(tenant.Admin.GetAsync($"{Members}?pageSize=0", _cancellationToken))).ShouldBe(HttpStatusCode.BadRequest);
    }

    public async ValueTask DisposeAsync() => await _data.DisposeAsync();

    private async Task<TenantWithAdministrator> NewTenantAsync()
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        var adminUserId = $"admin-{Guid.NewGuid():N}";
        var administratorRoleId = await _data.MakeAdministratorAsync(tenantId, adminUserId, _cancellationToken);
        var admin = factory.CreateAuthenticatedClient(TestTokens.Create(adminUserId, tenantId));
        return new TenantWithAdministrator(tenantId, adminUserId, administratorRoleId, admin);
    }

    private async Task<IReadOnlyList<AuditLogEntryResponse>> AuditEntriesAsync(HttpClient client, string action)
    {
        var page = await client.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            $"/api/v1/audit-log?action={action}", _cancellationToken);
        return page!.Items;
    }

    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return response.StatusCode;
    }

    private sealed record TenantWithAdministrator(Guid Id, string AdminUserId, Guid AdministratorRoleId, HttpClient Admin);
}
