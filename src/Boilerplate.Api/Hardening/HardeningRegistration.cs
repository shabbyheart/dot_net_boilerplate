using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Boilerplate.Api.Hardening;

/// <summary>
/// HTTP hardening (ADR-0024): trusted proxies, HSTS, security headers, CORS, rate limiting and Data Protection.
/// </summary>
internal static class HardeningRegistration
{
    /// <summary>The stricter per-IP limit for account endpoints (ADR-0025).</summary>
    public const string AccountsRateLimitPolicy = "accounts";

    private const string HealthPathPrefix = "/health";

    public static IServiceCollection AddApiHardening(this IServiceCollection services, IConfiguration configuration)
    {
        // Proxies: only configured addresses may set the client IP and scheme. Loopback stays trusted by default.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));

        // The host reads only endpoint settings from the "Kestrel" section; limits such as MaxRequestBodySize
        // must be bound explicitly or they are silently ignored.
        services.Configure<KestrelServerOptions>(configuration.GetSection("Kestrel"));

        // Browsers from these origins may call the API. Tokens travel in a header, so no credentials.
        // Read when the options are first used (like every setting here), not while services are registered.
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Delete)));

        services.AddOptions<RateLimitingSettings>()
            .BindConfiguration(RateLimitingSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitingSettings>>(ConfigureRateLimiting);

        // Nothing in this API uses Data Protection (no cookies or protected payloads); in-memory keys avoid
        // persisting keys nobody needs. A feature that needs it must persist and share keys first.
        services.AddDataProtection().UseEphemeralDataProtectionProvider();

        return services;
    }

    /// <summary>Adds the security headers just before each response is sent, so error responses get them too.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";

                // The Development-only API reference page needs scripts and styles.
                if (!context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase))
                {
                    headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
                }

                return Task.CompletedTask;
            });

            return next(context);
        });

    private static void ConfigureRateLimiting(RateLimiterOptions options, IOptions<RateLimitingSettings> settings)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (context.Request.Path.StartsWithSegments(HealthPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return RateLimitPartition.GetNoLimiter("health");
            }

            // Authenticated callers by token subject; anonymous callers by client IP (after forwarded headers).
            var subject = context.User.Identity?.IsAuthenticated == true
                ? context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                : null;
            var partition = subject is not null
                ? $"user:{subject}"
                : $"ip:{context.Connection.RemoteIpAddress}";

            return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.Value.PermitLimit,
                Window = settings.Value.Window,
                QueueLimit = 0,
            });
        });

        // Account requests are anonymous, so the budget is per client IP.
        options.AddPolicy(AccountsRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
            $"ip:{context.Connection.RemoteIpAddress}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.Value.AccountsPermitLimit,
                Window = settings.Value.AccountsWindow,
                QueueLimit = 0,
            }));

        options.OnRejected = async (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context.HttpContext,
                ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Detail = "Too many requests. Retry later." },
            });
        };
    }
}
