using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// The in-memory test server has no client IP. This sets it from the test-only <see cref="HeaderName"/> header,
/// before the application's pipeline, so proxy trust (forwarded headers) can be tested.
/// </summary>
internal sealed class TestRemoteIpStartupFilter : IStartupFilter
{
    public const string HeaderName = "X-Test-Remote-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(HeaderName, out var value) && IPAddress.TryParse(value, out var address))
            {
                context.Connection.RemoteIpAddress = address;
            }

            return nextMiddleware(context);
        });
        next(app);
    };
}
