using System.Globalization;
using Boilerplate.Api.ErrorHandling;
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

    var app = builder.Build();

    // Outermost, so the request summary records the final status code of handled errors.
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    // Liveness: the process can serve requests. Runs no dependency checks.
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

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
