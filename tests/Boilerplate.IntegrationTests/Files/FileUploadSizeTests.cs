using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Boilerplate.Application.Features.Files;
using Boilerplate.Application.Features.Projects;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests.Files;

/// <summary>
/// Only the upload endpoint accepts bodies above Kestrel's 1 MB limit, up to the maximum file size (ADR-0024, ADR-0030).
/// Needs a real server process: the in-memory test server does not enforce Kestrel limits.
/// </summary>
public sealed class FileUploadSizeTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const int MaxFileSize = 3 * 1024 * 1024;

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly AuthorizationData _data = new(postgres);
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"boilerplate-upload-size-{Guid.NewGuid():N}");

    [Fact]
    public async Task Upload_endpoint_accepts_files_up_to_the_maximum_and_rejects_larger_bodies_with_413()
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        await _data.GrantAsync(tenantId, "uploader", [FilePermissions.Manage, ProjectPermissions.Manage], _cancellationToken);

        var settings = new Dictionary<string, string?>(TestTokens.ApiSettings)
        {
            ["Database:ConnectionString"] = postgres.ConnectionString,
            ["Outbox:Enabled"] = "false",
            ["Housekeeping:Enabled"] = "false",
            ["Storage:Local:RootPath"] = _root,
            ["Storage:MaxFileSize"] = MaxFileSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        foreach (var (key, value) in PersistenceServices.UnusedSmtpSettings)
        {
            settings[key] = value;
        }

        await using var server = await ApiServerProcess.StartAsync(settings, _cancellationToken);
        using var client = new HttpClient { BaseAddress = server.BaseAddress };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create("uploader", tenantId));

        // Larger than the global 1 MB limit, within the file limit.
        using var accepted = await UploadAsync(client, 2 * 1024 * 1024);
        using var tooLarge = await UploadAsync(client, MaxFileSize + 512 * 1024, expectContinue: true);

        accepted.StatusCode.ShouldBe(HttpStatusCode.Created, await accepted.Content.ReadAsStringAsync(_cancellationToken));
        tooLarge.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        tooLarge.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        // The raised limit applies to the upload endpoint only.
        using var largeJsonRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects")
        {
            Content = JsonContent.Create(new { name = "Large", description = new string('x', 2 * 1024 * 1024) }),
        };
        largeJsonRequest.Headers.ExpectContinue = true;
        using var largeJson = await client.SendAsync(largeJsonRequest, _cancellationToken);
        largeJson.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    public async ValueTask DisposeAsync()
    {
        await _data.DisposeAsync();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // With Expect: 100-continue the client waits for the server before sending the body. Without it, Kestrel answers
    // 413 and closes the connection while the client is still sending, and the client sees a reset instead.
    private Task<HttpResponseMessage> UploadAsync(HttpClient client, int size, bool expectContinue = false)
    {
        var file = new ByteArrayContent(new byte[size]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files")
        {
            Content = new MultipartFormDataContent { { file, "file", "large.pdf" } },
        };
        request.Headers.ExpectContinue = expectContinue;
        return client.SendAsync(request, _cancellationToken);
    }
}
