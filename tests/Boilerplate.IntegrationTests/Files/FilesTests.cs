using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Boilerplate.Application.Common.Messaging;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Common.Storage;
using Boilerplate.Application.Features.Files;
using Boilerplate.Application.Features.Files.DeleteFile;
using Boilerplate.Application.Features.Projects;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Files;

/// <summary>Uploading, listing, downloading and deleting files (ADR-0030).</summary>
public sealed class FilesTests : IClassFixture<ApiFactory>, IAsyncDisposable
{
    private const string Files = "/api/v1/files";
    private const long MaxFileSize = 4096;

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"boilerplate-files-{Guid.NewGuid():N}");
    private readonly ApiFactory _factory;
    private readonly AuthorizationData _data;
    private readonly WebApplicationFactory<Program> _api;

    public FilesTests(ApiFactory factory, PostgresFixture postgres)
    {
        _factory = factory;
        _data = new AuthorizationData(postgres);
        _api = WithSettings(new Dictionary<string, string?>
        {
            ["Storage:Local:RootPath"] = _root,
            ["Storage:MaxFileSize"] = MaxFileSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
    }

    [Fact]
    public async Task Uploaded_file_downloads_as_an_attachment_with_the_same_content()
    {
        var tenant = await NewTenantAsync();
        var content = Encoding.UTF8.GetBytes("hello, files");

        using var response = await UploadAsync(tenant.Admin, content, "../notes.txt", "text/plain");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var file = (await response.Content.ReadFromJsonAsync<FileResponse>(_cancellationToken))!;
        file.FileName.ShouldBe("notes.txt");
        file.ContentType.ShouldBe("text/plain");
        file.Length.ShouldBe(content.Length);
        file.CreatedBy.ShouldBe(tenant.AdminUserId);
        response.Headers.Location!.AbsolutePath.ShouldBe($"{Files}/{file.Id}");

        using var download = await tenant.Admin.GetAsync($"{Files}/{file.Id}/content", _cancellationToken);

        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync(_cancellationToken)).ShouldBe(content);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        download.Content.Headers.ContentDisposition.FileNameStar.ShouldBe("notes.txt");
        download.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
    }

    [Fact]
    public async Task Lists_files_newest_first_and_gets_one()
    {
        var tenant = await NewTenantAsync();
        var first = await UploadFileAsync(tenant.Admin, "first.txt");
        var second = await UploadFileAsync(tenant.Admin, "second.txt");

        var page = await tenant.Admin.GetFromJsonAsync<PagedResponse<FileResponse>>(Files, _cancellationToken);

        page!.Items.Select(file => file.Id).ShouldBe([second.Id, first.Id]);
        (await tenant.Admin.GetFromJsonAsync<FileResponse>($"{Files}/{first.Id}", _cancellationToken)).ShouldBe(first);
    }

    [Fact]
    public async Task Deleted_file_is_gone_and_its_content_is_deleted_after_the_commit()
    {
        var tenant = await NewTenantAsync();
        var file = await UploadFileAsync(tenant.Admin, "doomed.txt");
        var path = Path.Combine(_root, file.Id.ToString("N"));
        File.Exists(path).ShouldBeTrue();

        (await StatusAsync(tenant.Admin.DeleteAsync($"{Files}/{file.Id}", _cancellationToken))).ShouldBe(HttpStatusCode.NoContent);

        (await StatusAsync(tenant.Admin.GetAsync($"{Files}/{file.Id}", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await StatusAsync(tenant.Admin.GetAsync($"{Files}/{file.Id}/content", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);

        // The dispatcher is off in tests: deliver the queued message the way it would, twice (at least once).
        var message = await QueuedDeletionAsync(file.Id.ToString("N"));
        await using var scope = _api.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IOutboxMessageHandler<DeleteFileContentMessage>>();
        var context = new OutboxMessageContext(Guid.NewGuid(), tenant.Id, 1);
        await handler.HandleAsync(message, context, _cancellationToken);
        await handler.HandleAsync(message, context, _cancellationToken);

        File.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_uploads_return_400_and_store_nothing()
    {
        var tenant = await NewTenantAsync();

        (await StatusAsync(UploadAsync(tenant.Admin, [1, 2, 3], "page.html", "text/html"))).ShouldBe(HttpStatusCode.BadRequest);
        (await StatusAsync(UploadAsync(tenant.Admin, [], "empty.txt", "text/plain"))).ShouldBe(HttpStatusCode.BadRequest);
        (await StatusAsync(UploadAsync(tenant.Admin, new byte[MaxFileSize + 1], "big.txt", "text/plain"))).ShouldBe(HttpStatusCode.BadRequest);

        using var noFile = new MultipartFormDataContent { { new StringContent("x"), "other" } };
        (await StatusAsync(tenant.Admin.PostAsync(Files, noFile, _cancellationToken))).ShouldBe(HttpStatusCode.BadRequest);
        (await StatusAsync(tenant.Admin.PostAsJsonAsync(Files, new { name = "x" }, _cancellationToken)))
            .ShouldBe(HttpStatusCode.UnsupportedMediaType);

        (await tenant.Admin.GetFromJsonAsync<PagedResponse<FileResponse>>(Files, _cancellationToken))!.TotalCount.ShouldBe(0);
        StoredContent().ShouldBeEmpty();
    }

    [Fact]
    public async Task Without_storage_configured_uploads_return_409_and_store_nothing()
    {
        await using var unconfigured = WithSettings(new Dictionary<string, string?>());
        var tenant = await NewTenantAsync(unconfigured);

        (await StatusAsync(UploadAsync(tenant.Admin, [1, 2, 3], "a.txt", "text/plain"))).ShouldBe(HttpStatusCode.Conflict);

        (await tenant.Admin.GetFromJsonAsync<PagedResponse<FileResponse>>(Files, _cancellationToken))!.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Endpoints_need_file_permissions_in_the_callers_tenant()
    {
        var tenant = await NewTenantAsync();
        var other = await NewTenantAsync();
        var file = await UploadFileAsync(tenant.Admin, "secret.txt");
        var userId = $"user-{Guid.NewGuid():N}";
        await _data.GrantAsync(tenant.Id, userId, [ProjectPermissions.Read], _cancellationToken);
        await _data.GrantAsync(other.Id, userId, [FilePermissions.Read, FilePermissions.Manage], _cancellationToken);
        using var withoutPermission = _api.CreateAuthenticatedClient(TestTokens.Create(userId, tenant.Id));
        var readerId = $"reader-{Guid.NewGuid():N}";
        await _data.GrantAsync(tenant.Id, readerId, [FilePermissions.Read], _cancellationToken);
        using var reader = _api.CreateAuthenticatedClient(TestTokens.Create(readerId, tenant.Id));
        using var anonymous = _api.CreateClient();

        (await StatusAsync(anonymous.GetAsync(Files, _cancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(anonymous.GetAsync($"{Files}/{file.Id}/content", _cancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(UploadAsync(anonymous, [1], "a.txt", "text/plain"))).ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(withoutPermission.GetAsync(Files, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(withoutPermission.GetAsync($"{Files}/{file.Id}/content", _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(reader.GetAsync($"{Files}/{file.Id}/content", _cancellationToken))).ShouldBe(HttpStatusCode.OK);
        (await StatusAsync(UploadAsync(reader, [1], "a.txt", "text/plain"))).ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(reader.DeleteAsync($"{Files}/{file.Id}", _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Files_of_other_tenants_are_not_found()
    {
        var tenantA = await NewTenantAsync();
        var tenantB = await NewTenantAsync();
        var fileOfA = await UploadFileAsync(tenantA.Admin, "a.txt");

        (await StatusAsync(tenantB.Admin.GetAsync($"{Files}/{fileOfA.Id}", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await StatusAsync(tenantB.Admin.GetAsync($"{Files}/{fileOfA.Id}/content", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await StatusAsync(tenantB.Admin.DeleteAsync($"{Files}/{fileOfA.Id}", _cancellationToken))).ShouldBe(HttpStatusCode.NotFound);
        (await tenantB.Admin.GetFromJsonAsync<PagedResponse<FileResponse>>(Files, _cancellationToken))!.TotalCount.ShouldBe(0);
        (await StatusAsync(tenantA.Admin.GetAsync($"{Files}/{fileOfA.Id}/content", _cancellationToken))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Storage_keys_cannot_leave_the_storage_root()
    {
        var storage = _api.Services.GetRequiredService<IFileStorage>();

        await Should.ThrowAsync<ArgumentException>(() => storage.WriteAsync("../outside", new MemoryStream([1]), _cancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => storage.OpenReadAsync(Path.Combine("..", "outside"), _cancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => storage.DeleteAsync(Path.GetFullPath(Path.Combine(_root, "..", "x")), _cancellationToken));
        (await storage.OpenReadAsync("missing", _cancellationToken)).ShouldBeNull();
    }

    public async ValueTask DisposeAsync()
    {
        await _api.DisposeAsync();
        await _data.DisposeAsync();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private WebApplicationFactory<Program> WithSettings(Dictionary<string, string?> settings) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings)));

    private Task<TenantWithAdministrator> NewTenantAsync() => NewTenantAsync(_api);

    private async Task<TenantWithAdministrator> NewTenantAsync(WebApplicationFactory<Program> api)
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        var adminUserId = $"admin-{Guid.NewGuid():N}";
        await _data.MakeAdministratorAsync(tenantId, adminUserId, _cancellationToken);
        return new TenantWithAdministrator(tenantId, adminUserId, api.CreateAuthenticatedClient(TestTokens.Create(adminUserId, tenantId)));
    }

    private Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] content, string fileName, string contentType)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", fileName } };
        return client.PostAsync(Files, form, _cancellationToken);
    }

    private async Task<FileResponse> UploadFileAsync(HttpClient client, string fileName)
    {
        using var response = await UploadAsync(client, Encoding.UTF8.GetBytes(fileName), fileName, "text/plain");
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(_cancellationToken));
        return (await response.Content.ReadFromJsonAsync<FileResponse>(_cancellationToken))!;
    }

    private async Task<DeleteFileContentMessage> QueuedDeletionAsync(string storageKey)
    {
        await using var scope = _api.Services.CreateAsyncScope();
        var payload = await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages
            .Where(message => message.Type == DeleteFileContentMessage.OutboxType && message.Payload.Contains(storageKey))
            .Select(message => message.Payload)
            .SingleAsync(_cancellationToken);
        return System.Text.Json.JsonSerializer.Deserialize<DeleteFileContentMessage>(payload, System.Text.Json.JsonSerializerOptions.Web)!;
    }

    private string[] StoredContent() =>
        Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories) : [];

    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return response.StatusCode;
    }

    private sealed record TenantWithAdministrator(Guid Id, string AdminUserId, HttpClient Admin);
}
