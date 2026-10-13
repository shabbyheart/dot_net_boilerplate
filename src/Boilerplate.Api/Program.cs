using System.Globalization;
using Boilerplate.Api.Authentication;
using Boilerplate.Api.ErrorHandling;
using Boilerplate.Api.Tenancy;
using Boilerplate.Application;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;

// Reports failures that happen before the host's logging is configured (ADR-0008).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Fail at startup on missing registrations or captive dependencies (ADR-0004).
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    });

    builder.Services.AddSerilog(
        (services, logger) =>
        {
            logger
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext();

            if (builder.Environment.IsDevelopment())
            {
                logger.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
            }
            else
            {
                logger.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
        },
        preserveStaticLogger: true);

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<ApiExceptionHandler>();
    builder.Services.AddHealthChecks();
    builder.Services.AddOpenApi();
    builder.Services.AddControllers();

    // Who is calling (ADR-0016) and for which tenant (ADR-0010); both are required by the database context.
    builder.Services.AddApiAuthentication();
    builder.Services.AddScoped<ITenantContext, ClaimsTenantContext>();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure();

    var app = builder.Build();

    // Outermost, so the request summary records the final status code of handled errors.
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();

    // Before authentication, so 401 and 403 challenges get a ProblemDetails body.
    app.UseStatusCodePages();

    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference().AllowAnonymous();
    }

    // Liveness: the process can serve requests. Runs no dependency checks.
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
        .AllowAnonymous();

    // Readiness: the dependencies needed to serve traffic (the database) are reachable.
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains(Boilerplate.Infrastructure.DependencyInjection.ReadyHealthCheckTag),
    }).AllowAnonymous();

    app.MapControllers();

    await app.RunAsync();
    return 0;
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    // HostAbortedException is how test and EF tooling stop the host on purpose; it is not a failure.
    Log.Fatal(exception, "Host terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
