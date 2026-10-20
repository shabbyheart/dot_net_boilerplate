using System.Net;
using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests.Platform;

/// <summary>The one-off command is the only way to grant or revoke platform administration (ADR-0027).</summary>
public sealed class PlatformAdministratorCommandTests(ApiFactory factory, PostgresFixture postgres) : IClassFixture<ApiFactory>
{
    private const string Tenants = "/api/v1/platform/tenants";

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Grant_and_revoke_take_effect_and_can_be_repeated()
    {
        var userId = $"platform-{Guid.NewGuid():N}";
        using var client = factory.CreateAuthenticatedClient(TestTokens.Create(userId));

        (await RunAsync("grant", "--user", userId)).ExitCode.ShouldBe(0);
        (await RunAsync("grant", "--user", userId)).ExitCode.ShouldBe(0);
        (await StatusAsync(client.GetAsync(Tenants, _cancellationToken))).ShouldBe(HttpStatusCode.OK);

        (await RunAsync("revoke", "--user", userId)).ExitCode.ShouldBe(0);
        (await RunAsync("revoke", "--user", userId)).ExitCode.ShouldBe(0);
        (await StatusAsync(client.GetAsync(Tenants, _cancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
    }

    public static TheoryData<string[]> InvalidOptions => new()
    {
        { [] },
        { ["promote", "--user", "someone"] },
        { ["grant"] },
        { ["grant", "--user", " "] },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public async Task Invalid_arguments_exit_with_code_2(string[] options)
    {
        var result = await RunAsync(options);

        result.ExitCode.ShouldBe(2, result.Output);
    }

    private Task<ApiProcess.Result> RunAsync(params string[] options) =>
        ApiProcess.RunAsync(
            new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["Database__ConnectionString"] = postgres.ConnectionString,
            },
            ["platform-admin", .. options],
            _cancellationToken);

    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return response.StatusCode;
    }
}
