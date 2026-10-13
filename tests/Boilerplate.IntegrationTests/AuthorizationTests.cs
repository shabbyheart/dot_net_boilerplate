using System.Net;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests;

/// <summary>Permission enforcement and immediate revocation (ADR-0017), within the caller's tenant (ADR-0010).</summary>
public sealed class AuthorizationTests(ApiFactory factory, PostgresFixture postgres) : IClassFixture<ApiFactory>, IAsyncDisposable
{
    private const string Permission = PermissionProbeController.Permission;

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly AuthorizationData _data = new(postgres);

    [Fact]
    public async Task Anonymous_caller_gets_401()
    {
        using var client = factory.CreateClient();

        (await GetStatusAsync(client)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Caller_without_a_tenant_gets_403()
    {
        using var client = factory.CreateAuthenticatedClient(TestTokens.Create(subject: "no-tenant-user"));

        (await GetStatusAsync(client)).ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Caller_without_the_permission_gets_403_problem()
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        await _data.GrantAsync(tenantId, "user-other-permission", ["probe.write"], _cancellationToken);
        using var client = ClientFor("user-other-permission", tenantId);

        using var response = await client.GetAsync(PermissionProbeController.Path, _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Caller_with_the_permission_through_a_role_is_allowed()
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        await _data.GrantAsync(tenantId, "user-granted", [Permission], _cancellationToken);
        using var client = ClientFor("user-granted", tenantId);

        (await GetStatusAsync(client)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Revoking_the_permission_takes_effect_on_the_next_request()
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        var roleId = await _data.GrantAsync(tenantId, "user-revoked", [Permission], _cancellationToken);
        using var client = ClientFor("user-revoked", tenantId);
        (await GetStatusAsync(client)).ShouldBe(HttpStatusCode.OK);

        await _data.RevokeAsync(tenantId, roleId, Permission, _cancellationToken);

        (await GetStatusAsync(client)).ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Removing_the_role_assignment_takes_effect_on_the_next_request()
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        var roleId = await _data.GrantAsync(tenantId, "user-unassigned", [Permission], _cancellationToken);
        using var client = ClientFor("user-unassigned", tenantId);
        (await GetStatusAsync(client)).ShouldBe(HttpStatusCode.OK);

        await _data.UnassignAsync(tenantId, "user-unassigned", roleId, _cancellationToken);

        (await GetStatusAsync(client)).ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Revocation_is_immediate_on_every_instance()
    {
        // ADR-0017 (A15): two API instances share the database; neither may keep serving a revoked permission.
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        var roleId = await _data.GrantAsync(tenantId, "user-two-instances", [Permission], _cancellationToken);
        await using var secondInstance = new ApiFactory(postgres);
        using var clientA = ClientFor("user-two-instances", tenantId);
        using var clientB = secondInstance.CreateAuthenticatedClient(TestTokens.Create("user-two-instances", tenantId));
        (await GetStatusAsync(clientA)).ShouldBe(HttpStatusCode.OK);
        (await GetStatusAsync(clientB)).ShouldBe(HttpStatusCode.OK);

        await _data.RevokeAsync(tenantId, roleId, Permission, _cancellationToken);

        (await GetStatusAsync(clientA)).ShouldBe(HttpStatusCode.Forbidden);
        (await GetStatusAsync(clientB)).ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Roles_in_another_tenant_do_not_apply()
    {
        var tenantA = await _data.CreateTenantAsync(_cancellationToken);
        var tenantB = await _data.CreateTenantAsync(_cancellationToken);
        await _data.GrantAsync(tenantA, "user-multi-tenant", [Permission], _cancellationToken);

        using var inTenantA = ClientFor("user-multi-tenant", tenantA);
        using var inTenantB = ClientFor("user-multi-tenant", tenantB);

        (await GetStatusAsync(inTenantA)).ShouldBe(HttpStatusCode.OK);
        (await GetStatusAsync(inTenantB)).ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Another_users_role_does_not_apply()
    {
        var tenantId = await _data.CreateTenantAsync(_cancellationToken);
        await _data.GrantAsync(tenantId, "user-with-role", [Permission], _cancellationToken);
        using var client = ClientFor("user-without-role", tenantId);

        (await GetStatusAsync(client)).ShouldBe(HttpStatusCode.Forbidden);
    }

    public ValueTask DisposeAsync() => _data.DisposeAsync();

    private HttpClient ClientFor(string userId, Guid tenantId) =>
        factory.CreateAuthenticatedClient(TestTokens.Create(userId, tenantId));

    private async Task<HttpStatusCode> GetStatusAsync(HttpClient client)
    {
        using var response = await client.GetAsync(PermissionProbeController.Path, _cancellationToken);
        return response.StatusCode;
    }
}
