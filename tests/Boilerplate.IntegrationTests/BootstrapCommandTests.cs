using System.Net;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests;

/// <summary>The one-off bootstrap command creates a tenant and its first administrator (ADR-0022).</summary>
public sealed class BootstrapCommandTests(ApiFactory factory, PostgresFixture postgres) : IClassFixture<ApiFactory>, IAsyncDisposable
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly AuthorizationData _data = new(postgres);

    [Fact]
    public async Task Creates_the_tenant_and_an_administrator_who_can_administer_it()
    {
        var tenantName = $"bootstrap-{Guid.NewGuid():N}";

        var result = await BootstrapAsync("--tenant-name", tenantName, "--admin-user", "first-admin");

        result.ExitCode.ShouldBe(0, result.Output);
        var tenant = await _data.FindTenantAsync(tenantName, _cancellationToken);
        tenant.ShouldNotBeNull();
        var (roles, administrators) = await _data.AdministratorsAsync(tenant.Id, _cancellationToken);
        roles.ShouldBe(1);
        administrators.ShouldBe(["first-admin"]);

        using var admin = factory.CreateAuthenticatedClient(TestTokens.Create("first-admin", tenant.Id));
        using var response = await admin.GetAsync("/api/v1/roles", _cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Running_again_changes_nothing_and_can_add_another_administrator()
    {
        var tenantName = $"bootstrap-{Guid.NewGuid():N}";

        (await BootstrapAsync("--tenant-name", tenantName, "--admin-user", "first-admin")).ExitCode.ShouldBe(0);
        (await BootstrapAsync("--tenant-name", tenantName, "--admin-user", "first-admin")).ExitCode.ShouldBe(0);
        (await BootstrapAsync("--tenant-name", $"  {tenantName}  ", "--admin-user", "second-admin")).ExitCode.ShouldBe(0);

        var tenant = await _data.FindTenantAsync(tenantName, _cancellationToken);
        var (roles, administrators) = await _data.AdministratorsAsync(tenant!.Id, _cancellationToken);
        roles.ShouldBe(1);
        administrators.ShouldBe(["first-admin", "second-admin"], ignoreOrder: true);
    }

    public static TheoryData<string[]> InvalidOptions => new()
    {
        { ["--tenant-name", "Acme"] },
        { ["--admin-user", "someone"] },
        { ["--tenant-name", " ", "--admin-user", "someone"] },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public async Task Invalid_arguments_exit_with_code_2(string[] options)
    {
        var result = await BootstrapAsync(options);

        result.ExitCode.ShouldBe(2, result.Output);
    }

    public ValueTask DisposeAsync() => _data.DisposeAsync();

    private Task<ApiProcess.Result> BootstrapAsync(params string[] options) =>
        ApiProcess.RunAsync(
            new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["Database__ConnectionString"] = postgres.ConnectionString,
            },
            ["bootstrap", .. options],
            _cancellationToken);
}
