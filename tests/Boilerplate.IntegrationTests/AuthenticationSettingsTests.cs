using Boilerplate.IntegrationTests.Support;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Boilerplate.IntegrationTests;

/// <summary>Effective token validation settings for an external-provider configuration (ADR-0016).</summary>
public sealed class AuthenticationSettingsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Authority_configuration_validates_everything_and_keeps_jwt_claim_names()
    {
        await using var externalProvider = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Signing keys are only loaded for configured issuers, so clearing the issuer also drops the test key.
                    ["Authentication:Schemes:Bearer:Authority"] = "https://issuer.example",
                    ["Authentication:Schemes:Bearer:ValidIssuer"] = string.Empty,
                })));

        var options = externalProvider.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var parameters = options.TokenValidationParameters;

        options.Authority.ShouldBe("https://issuer.example");
        parameters.ValidateIssuer.ShouldBeTrue();
        parameters.ValidateAudience.ShouldBeTrue();
        parameters.ValidateLifetime.ShouldBeTrue();
        parameters.ValidateIssuerSigningKey.ShouldBeTrue();

        // Set only by AuthenticationRegistration, so these catch a lost post-configuration step.
        options.MapInboundClaims.ShouldBeFalse();
        parameters.ClockSkew.ShouldBe(TimeSpan.FromMinutes(1));
    }
}
