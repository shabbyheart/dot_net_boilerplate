using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Common.Security;
using Boilerplate.Application.Features.Projects;
using Boilerplate.Application.Features.Roles;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests;

/// <summary>Role administration, the escalation guard and the administrator role (ADR-0022).</summary>
public sealed class RolesTests(ApiFactory factory, PostgresFixture postgres) : IClassFixture<ApiFactory>, IAsyncDisposable
{
    private const string Roles = "/api/v1/roles";
    private const string Projects = "/api/v1/projects";

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly AuthorizationData _data = new(postgres);

    [Fact]
    public async Task Administrator_creates_reads_and_lists_roles()
    {
        var tenant = await NewTenantWithAdministratorAsync();

        using var created = await tenant.Admin.PostAsJsonAsync(
            Roles,
            new SaveRoleRequest("Editors", [ProjectPermissions.Manage, ProjectPermissions.Read]),
            _cancellationToken);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var role = (await created.Content.ReadFromJsonAsync<RoleResponse>(_cancellationToken))!;
        role.IsAdministrator.ShouldBeFalse();
        role.Permissions.ShouldBe([ProjectPermissions.Manage, ProjectPermissions.Read]);

        // Records compare lists by reference, so compare the content structurally.
        (await tenant.Admin.GetFromJsonAsync<RoleResponse>($"{Roles}/{role.Id}", _cancellationToken)).ShouldBeEquivalentTo(role);

        var list = await tenant.Admin.GetFromJsonAsync<PagedResponse<RoleResponse>>(Roles, _cancellationToken);
        list!.Items.Select(r => r.Name).ShouldBe([Domain.Authorization.Role.AdministratorsName, "Editors"]);
        list.Items[0].IsAdministrator.ShouldBeTrue();
    }

    [Fact]
    public async Task Permission_catalog_lists_every_permission()
    {
        var tenant = await NewTenantWithAdministratorAsync();

        var catalog = await tenant.Admin.GetFromJsonAsync<string[]>("/api/v1/permissions", _cancellationToken);

        catalog.ShouldBe(PermissionCatalog.All);
    }

    [Fact]
    public async Task Unknown_permission_is_rejected_with_400()
    {
        var tenant = await NewTenantWithAdministratorAsync();

        using var response = await tenant.Admin.PostAsJsonAsync(
            Roles,
            new SaveRoleRequest("Editors", ["made.up"]),
            _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response)).ShouldContainKey("permissions[0]");
    }

    [Fact]
    public async Task Duplicate_role_name_returns_409()
    {
        var tenant = await NewTenantWithAdministratorAsync();
        await CreateRoleAsync(tenant.Admin, "Editors", []);

        using var response = await tenant.Admin.PostAsJsonAsync(Roles, new SaveRoleRequest("Editors", []), _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Manager_cannot_grant_a_permission_they_do_not_hold()
    {
        var tenant = await NewTenantWithAdministratorAsync();
        using var manager = await ManagerAsync(tenant.Id, [RolePermissions.Manage, RolePermissions.Read, ProjectPermissions.Read]);

        using var escalation = await manager.PostAsJsonAsync(
            Roles,
            new SaveRoleRequest("Escalated", [ProjectPermissions.Manage]),
            _cancellationToken);
        using var allowed = await manager.PostAsJsonAsync(
            Roles,
            new SaveRoleRequest("Readers", [ProjectPermissions.Read]),
            _cancellationToken);

        escalation.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        escalation.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        allowed.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Manager_cannot_remove_a_permission_they_do_not_hold_but_may_rename()
    {
        var tenant = await NewTenantWithAdministratorAsync();
        var role = await CreateRoleAsync(tenant.Admin, "Editors", [ProjectPermissions.Manage]);
        using var manager = await ManagerAsync(tenant.Id, [RolePermissions.Manage, RolePermissions.Read]);

        using var removal = await manager.PutAsJsonAsync($"{Roles}/{role.Id}", new SaveRoleRequest("Editors", []), _cancellationToken);
        using var rename = await manager.PutAsJsonAsync(
            $"{Roles}/{role.Id}",
            new SaveRoleRequest("Content editors", [ProjectPermissions.Manage]),
            _cancellationToken);

        removal.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        rename.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Assigning_a_role_grants_access_and_unassigning_revokes_it()
    {
        var tenant = await NewTenantWithAdministratorAsync();
        var role = await CreateRoleAsync(tenant.Admin, "Readers", [ProjectPermissions.Read]);
        var userId = $"user-{Guid.NewGuid():N}";
        using var user = factory.CreateAuthenticatedClient(TestTokens.Create(userId, tenant.Id));
        (await StatusAsync(user.GetAsync(Projects, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);

        (await StatusAsync(tenant.Admin.PutAsync($"{Roles}/{role.Id}/assignments/{userId}", null, _cancellationToken)))
            .ShouldBe(HttpStatusCode.NoContent);
        (await StatusAsync(tenant.Admin.PutAsync($"{Roles}/{role.Id}/assignments/{userId}", null, _cancellationToken)))
            .ShouldBe(HttpStatusCode.NoContent);

        (await StatusAsync(user.GetAsync(Projects, _cancellationToken))).ShouldBe(HttpStatusCode.OK);
        var assignments = await tenant.Admin.GetFromJsonAsync<PagedResponse<RoleAssignmentResponse>>(
            $"{Roles}/{role.Id}/assignments",
            _cancellationToken);
        assignments!.Items.Select(a => a.UserId).ShouldBe([userId]);
        assignments.Items[0].AssignedBy.ShouldBe(tenant.AdminUserId);

        (await StatusAsync(tenant.Admin.DeleteAsync($"{Roles}/{role.Id}/assignments/{userId}", _cancellationToken)))
            .ShouldBe(HttpStatusCode.NoContent);
        (await StatusAsync(user.GetAsync(Projects, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(tenant.Admin.DeleteAsync($"{Roles}/{role.Id}/assignments/{userId}", _cancellationToken)))
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Manager_cannot_hand_out_a_role_with_permissions_they_do_not_hold()
    {
        var tenant = await NewTenantWithAdministratorAsync();
        var role = await CreateRoleAsync(tenant.Admin, "Editors", [ProjectPermissions.Manage]);
        using var manager = await ManagerAsync(tenant.Id, [RolePermissions.Manage, RolePermissions.Read]);

        (await StatusAsync(manager.PutAsync($"{Roles}/{role.Id}/assignments/someone", null, _cancellationToken)))
            .ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(manager.DeleteAsync($"{Roles}/{role.Id}", _cancellationToken)))
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deleting_a_role_revokes_it_from_its_holders()
    {
        var tenant = await NewTenantWithAdministratorAsync();
        var role = await CreateRoleAsync(tenant.Admin, "Readers", [ProjectPermissions.Read]);
        var userId = $"user-{Guid.NewGuid():N}";
        (await tenant.Admin.PutAsync($"{Roles}/{role.Id}/assignments/{userId}", null, _cancellationToken)).Dispose();
        using var user = factory.CreateAuthenticatedClient(TestTokens.Create(userId, tenant.Id));
        (await StatusAsync(user.GetAsync(Projects, _cancellationToken))).ShouldBe(HttpStatusCode.OK);

        (await StatusAsync(tenant.Admin.DeleteAsync($"{Roles}/{role.Id}", _cancellationToken))).ShouldBe(HttpStatusCode.NoContent);

        (await StatusAsync(user.GetAsync(Projects, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Administrators_hold_every_permission_without_explicit_grants()
    {
        var tenant = await NewTenantWithAdministratorAsync();

        (await StatusAsync(tenant.Admin.GetAsync(Projects, _cancellationToken))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Administrator_role_cannot_be_changed_or_deleted()
    {
        var tenant = await NewTenantWithAdministratorAsync();
        var url = $"{Roles}/{tenant.AdministratorRoleId}";

        (await StatusAsync(tenant.Admin.PutAsJsonAsync(url, new SaveRoleRequest("Admins", []), _cancellationToken)))
            .ShouldBe(HttpStatusCode.BadRequest);
        (await StatusAsync(tenant.Admin.DeleteAsync(url, _cancellationToken))).ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Only_administrators_manage_the_administrator_role_and_the_last_one_stays()
    {
        var tenant = await NewTenantWithAdministratorAsync();
        var assignments = $"{Roles}/{tenant.AdministratorRoleId}/assignments";
        using var manager = await ManagerAsync(tenant.Id, [RolePermissions.Manage, RolePermissions.Read]);

        (await StatusAsync(manager.PutAsync($"{assignments}/someone", null, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);

        (await StatusAsync(tenant.Admin.DeleteAsync($"{assignments}/{tenant.AdminUserId}", _cancellationToken)))
            .ShouldBe(HttpStatusCode.Conflict);

        (await StatusAsync(tenant.Admin.PutAsync($"{assignments}/second-admin", null, _cancellationToken)))
            .ShouldBe(HttpStatusCode.NoContent);
        (await StatusAsync(tenant.Admin.DeleteAsync($"{assignments}/{tenant.AdminUserId}", _cancellationToken)))
            .ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Another_tenants_role_is_not_found()
    {
        var tenantA = await NewTenantWithAdministratorAsync();
        var tenantB = await NewTenantWithAdministratorAsync();
        var roleOfA = await CreateRoleAsync(tenantA.Admin, "Editors", [ProjectPermissions.Read]);

        (await StatusAsync(tenantB.Admin.GetAsync($"{Roles}/{roleOfA.Id}", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await StatusAsync(tenantB.Admin.DeleteAsync($"{Roles}/{roleOfA.Id}", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await StatusAsync(tenantB.Admin.PutAsync($"{Roles}/{roleOfA.Id}/assignments/someone", null, _cancellationToken)))
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Anonymous_caller_gets_401_and_caller_without_permission_gets_403()
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        using var anonymous = factory.CreateClient();
        using var withoutPermission = factory.CreateAuthenticatedClient(TestTokens.Create("nobody", tenantId));

        (await StatusAsync(anonymous.GetAsync(Roles, _cancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(withoutPermission.GetAsync(Roles, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
    }

    public async ValueTask DisposeAsync() => await _data.DisposeAsync();

    private async Task<TenantWithAdministrator> NewTenantWithAdministratorAsync()
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        var adminUserId = $"admin-{Guid.NewGuid():N}";
        var administratorRoleId = await _data.MakeAdministratorAsync(tenantId, adminUserId, _cancellationToken);
        var admin = factory.CreateAuthenticatedClient(TestTokens.Create(adminUserId, tenantId));
        return new TenantWithAdministrator(tenantId, adminUserId, administratorRoleId, admin);
    }

    private async Task<HttpClient> ManagerAsync(Guid tenantId, string[] permissions)
    {
        var userId = $"manager-{Guid.NewGuid():N}";
        await _data.GrantAsync(tenantId, userId, permissions, _cancellationToken);
        return factory.CreateAuthenticatedClient(TestTokens.Create(userId, tenantId));
    }

    private async Task<RoleResponse> CreateRoleAsync(HttpClient client, string name, string[] permissions)
    {
        using var response = await client.PostAsJsonAsync(Roles, new SaveRoleRequest(name, permissions), _cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<RoleResponse>(_cancellationToken))!;
    }

    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return response.StatusCode;
    }

    private async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_cancellationToken));
        return problem.RootElement.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }

    private sealed record TenantWithAdministrator(Guid Id, string AdminUserId, Guid AdministratorRoleId, HttpClient Admin);
}
