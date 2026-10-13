using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests;

/// <summary>
/// Invalid configuration stops the real process with exit code 1 and logs the reason (ADR-0007).
/// Runs the API as a child process because the exit code is only observable there.
/// </summary>
public sealed class StartupFailureTests
{
    private const string ValidConnectionString = "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused";

    [Theory]
    [InlineData("Database__ConnectionString", "", "The ConnectionString field is required.")]
    [InlineData("Serilog__MinimumLevel__Default", "NotALevel", "The value NotALevel is not a valid Serilog level.")]
    [InlineData(
        "Authentication__Schemes__Bearer__Authority",
        "",
        "Authentication:Schemes:Bearer needs either Authority, or ValidIssuer together with SigningKeys.")]
    [InlineData("Authentication__Schemes__Bearer__ValidAudiences__0", "", "Authentication:Schemes:Bearer:ValidAudiences is required.")]
    [InlineData("Email__Smtp__FromAddress", "", "The FromAddress field is required.")]
    [InlineData("Outbox__BatchSize", "0", "The field BatchSize must be between 1 and 500.")]
    public async Task Host_exits_with_code_1_when_configuration_is_invalid(string setting, string value, string expectedReason)
    {
        // A valid baseline, so the setting under test is the only reason for the failure.
        var environment = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["ASPNETCORE_URLS"] = "http://127.0.0.1:0",
            ["Database__ConnectionString"] = ValidConnectionString,
            ["Authentication__Schemes__Bearer__Authority"] = "https://issuer.example",
            ["Authentication__Schemes__Bearer__ValidAudiences__0"] = TestTokens.Audience,
            ["Email__Smtp__Host"] = "smtp.invalid",
            ["Email__Smtp__FromAddress"] = "noreply@tests.boilerplate.local",
        };
        environment[setting] = value;

        var result = await ApiProcess.RunAsync(environment, [], TestContext.Current.CancellationToken);

        result.ExitCode.ShouldBe(1, result.Output);
        result.Output.ShouldContain("Host terminated unexpectedly");
        result.Output.ShouldContain(expectedReason);
    }
}
