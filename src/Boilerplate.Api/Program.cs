using System.Globalization;
using Boilerplate.Api.Authentication;
using Boilerplate.Api.Commands;
using Boilerplate.Api.ErrorHandling;
using Boilerplate.Api.Hardening;
using Boilerplate.Api.Registration;
using Boilerplate.Api.Tenancy;
using Boilerplate.Api.Validation;
using Boilerplate.Application;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
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
    // FluentValidation is the single source of request rules (ADR-0014): no implicit [Required] for non-nullable strings.
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationFilter>();
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    });

    // Who is calling (ADR-0016) and for which tenant (ADR-0010); both are required by the database context.
    builder.Services.AddApiAuthentication();
    builder.Services.AddScoped<ITenantContext, ClaimsTenantContext>();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure();

    // Operational hardening: tracing and metrics (ADR-0023), proxies, headers, CORS, rate limits (ADR-0024).
    builder.Services.AddApiObservability(builder.Configuration);
    builder.Services.AddApiHardening(builder.Configuration);

    var app = builder.Build();

    // One-off commands use the same configuration and services but never start the web server (ADR-0022).
    if (BootstrapCommand.IsRequested(args))
    {
        return await BootstrapCommand.RunAsync(app.Services, args, CancellationToken.None);
    }

    // First: everything after it must see the real client IP and scheme (only from trusted proxies).
    app.UseForwardedHeaders();

    // Outermost logging, so the request summary records the final status code of handled errors.
    app.UseSerilogRequestLogging(options => options.GetLevel = RequestLogLevel);
    app.UseSecurityHeaders();
    app.UseExceptionHandler();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    // Before authentication, so 401, 403 and 429 responses get a ProblemDetails body.
    app.UseStatusCodePages();

    // Before authentication, so browser preflight requests are answered without a token.
    app.UseCors();

    app.UseAuthentication();

    // After authentication (callers are limited per user) and before authorization (rejected work is not done).
    app.UseRateLimiter();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference().AllowAnonymous();
    }

    // Liveness: the process can serve requests. Runs no dependency checks.
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
        .AllowAnonymous();

    // Readiness: the database is reachable and has every migration this code expects (ADR-0024).
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

// Probes run every few seconds; their request summaries would drown real traffic (ADR-0024).
// Otherwise the Serilog default: Error for exceptions and 5xx, Information for the rest.
static LogEventLevel RequestLogLevel(HttpContext context, double elapsedMilliseconds, Exception? exception)
{
    if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
    {
        return LogEventLevel.Debug;
    }

    return exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError
        ? LogEventLevel.Error
        : LogEventLevel.Information;
}
