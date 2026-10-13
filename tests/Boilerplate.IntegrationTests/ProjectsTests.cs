using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Features.Projects;
using Boilerplate.Application.Features.Projects.CreateProject;
using Boilerplate.Application.Features.Projects.UpdateProject;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests;

/// <summary>
/// The reference feature end to end: CRUD, validation, errors, paging, permissions and tenant isolation.
/// New features copy this test structure (docs/how-to-add-a-feature.md).
/// </summary>
public sealed class ProjectsTests(ApiFactory factory, PostgresFixture postgres) : IClassFixture<ApiFactory>, IAsyncDisposable
{
    private const string Path = "/api/v1/projects";
    private static readonly string[] ReadAndManage = [ProjectPermissions.Read, ProjectPermissions.Manage];

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly AuthorizationData _data = new(postgres);

    [Fact]
    public async Task Create_get_update_and_delete_a_project()
    {
        using var client = await ClientForNewTenantAsync(ReadAndManage);

        using var created = await client.PostAsJsonAsync(Path, new CreateProjectRequest("Website", "Public site"), _cancellationToken);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = (await created.Content.ReadFromJsonAsync<ProjectResponse>(_cancellationToken))!;
        project.Name.ShouldBe("Website");
        project.Description.ShouldBe("Public site");
        created.Headers.Location!.AbsolutePath.ShouldBe($"{Path}/{project.Id}");

        var fetched = await client.GetFromJsonAsync<ProjectResponse>($"{Path}/{project.Id}", _cancellationToken);
        fetched.ShouldBe(project);

        using var updated = await client.PutAsJsonAsync($"{Path}/{project.Id}", new UpdateProjectRequest("Web shop", null), _cancellationToken);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterUpdate = (await updated.Content.ReadFromJsonAsync<ProjectResponse>(_cancellationToken))!;
        afterUpdate.Name.ShouldBe("Web shop");
        afterUpdate.Description.ShouldBeNull();
        afterUpdate.CreatedAt.ShouldBe(project.CreatedAt);

        using var deleted = await client.DeleteAsync($"{Path}/{project.Id}", _cancellationToken);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var afterDelete = await client.GetAsync($"{Path}/{project.Id}", _cancellationToken);
        afterDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_create_request_returns_400_with_errors_per_field()
    {
        using var client = await ClientForNewTenantAsync(ReadAndManage);

        using var response = await client.PostAsJsonAsync(
            Path,
            new CreateProjectRequest("", new string('d', 2001)),
            _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var errors = await ErrorsAsync(response);
        errors.ShouldContainKey("name");
        errors.ShouldContainKey("description");
    }

    [Fact]
    public async Task Missing_name_is_reported_by_the_same_validation()
    {
        using var client = await ClientForNewTenantAsync(ReadAndManage);

        using var response = await client.PostAsJsonAsync(Path, new { description = "No name" }, _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response)).Keys.ShouldBe(["name"]);
    }

    [Fact]
    public async Task Duplicate_name_in_the_same_tenant_returns_409()
    {
        using var client = await ClientForNewTenantAsync(ReadAndManage);
        (await client.PostAsJsonAsync(Path, new CreateProjectRequest("Website", null), _cancellationToken)).Dispose();

        using var duplicate = await client.PostAsJsonAsync(Path, new CreateProjectRequest(" Website ", null), _cancellationToken);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Renaming_to_an_existing_name_returns_409()
    {
        using var client = await ClientForNewTenantAsync(ReadAndManage);
        (await client.PostAsJsonAsync(Path, new CreateProjectRequest("Website", null), _cancellationToken)).Dispose();
        var other = await CreateAsync(client, "Shop");

        using var response = await client.PutAsJsonAsync($"{Path}/{other.Id}", new UpdateProjectRequest("Website", null), _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task The_same_name_is_allowed_in_another_tenant()
    {
        using var tenantA = await ClientForNewTenantAsync(ReadAndManage);
        using var tenantB = await ClientForNewTenantAsync(ReadAndManage);
        await CreateAsync(tenantA, "Website");

        using var response = await tenantB.PostAsJsonAsync(Path, new CreateProjectRequest("Website", null), _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Another_tenants_project_is_not_found()
    {
        using var tenantA = await ClientForNewTenantAsync(ReadAndManage);
        using var tenantB = await ClientForNewTenantAsync(ReadAndManage);
        var projectOfA = await CreateAsync(tenantA, "Secret");
        var url = $"{Path}/{projectOfA.Id}";

        (await StatusAsync(tenantB.GetAsync(url, _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await StatusAsync(tenantB.PutAsJsonAsync(url, new UpdateProjectRequest("Taken", null), _cancellationToken)))
            .ShouldBe(HttpStatusCode.NotFound);
        (await StatusAsync(tenantB.DeleteAsync(url, _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);

        var stillThere = await tenantA.GetFromJsonAsync<ProjectResponse>(url, _cancellationToken);
        stillThere!.Name.ShouldBe("Secret");
    }

    [Fact]
    public async Task List_returns_only_the_tenants_projects_paged_and_ordered_by_name()
    {
        using var tenantA = await ClientForNewTenantAsync(ReadAndManage);
        using var tenantB = await ClientForNewTenantAsync(ReadAndManage);
        await CreateAsync(tenantA, "Charlie");
        await CreateAsync(tenantA, "Alpha");
        await CreateAsync(tenantA, "Bravo");
        await CreateAsync(tenantB, "Other tenant");

        var firstPage = await tenantA.GetFromJsonAsync<PagedResponse<ProjectResponse>>($"{Path}?page=1&pageSize=2", _cancellationToken);
        var secondPage = await tenantA.GetFromJsonAsync<PagedResponse<ProjectResponse>>($"{Path}?page=2&pageSize=2", _cancellationToken);

        firstPage!.TotalCount.ShouldBe(3);
        firstPage.Items.Select(project => project.Name).ShouldBe(["Alpha", "Bravo"]);
        secondPage!.Items.Select(project => project.Name).ShouldBe(["Charlie"]);
        secondPage.Page.ShouldBe(2);
        secondPage.PageSize.ShouldBe(2);
    }

    [Fact]
    public async Task List_uses_default_page_size()
    {
        using var client = await ClientForNewTenantAsync(ReadAndManage);

        var page = await client.GetFromJsonAsync<PagedResponse<ProjectResponse>>(Path, _cancellationToken);

        page!.Page.ShouldBe(1);
        page.PageSize.ShouldBe(PageRequest.DefaultPageSize);
        page.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("page=0", "page")]
    public async Task List_rejects_page_parameters_out_of_range(string query, string field)
    {
        using var client = await ClientForNewTenantAsync(ReadAndManage);

        using var response = await client.GetAsync($"{Path}?{query}", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response)).ShouldContainKey(field);
    }

    [Fact]
    public async Task Anonymous_caller_gets_401()
    {
        using var client = factory.CreateClient();

        (await StatusAsync(client.GetAsync(Path, _cancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Read_permission_does_not_allow_changes()
    {
        using var client = await ClientForNewTenantAsync([ProjectPermissions.Read]);

        (await StatusAsync(client.GetAsync(Path, _cancellationToken))).ShouldBe(HttpStatusCode.OK);
        (await StatusAsync(client.PostAsJsonAsync(Path, new CreateProjectRequest("Website", null), _cancellationToken)))
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Caller_without_project_permissions_gets_403()
    {
        using var client = await ClientForNewTenantAsync([]);

        (await StatusAsync(client.GetAsync(Path, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
    }

    public ValueTask DisposeAsync() => _data.DisposeAsync();

    private async Task<HttpClient> ClientForNewTenantAsync(string[] permissions)
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        var userId = $"user-{Guid.NewGuid():N}";
        if (permissions.Length > 0)
        {
            await _data.GrantAsync(tenantId, userId, permissions, _cancellationToken);
        }

        return factory.CreateAuthenticatedClient(TestTokens.Create(userId, tenantId));
    }

    private async Task<ProjectResponse> CreateAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(Path, new CreateProjectRequest(name, null), _cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(_cancellationToken))!;
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
}
