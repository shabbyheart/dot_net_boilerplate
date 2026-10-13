using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// Adds routes that throw, so tests can exercise the real exception handling pipeline.
/// The middleware runs after the application's own pipeline, i.e. inside its exception handler,
/// and only sees requests that no real endpoint handled.
/// </summary>
internal sealed class ThrowingRoutesStartupFilter : IStartupFilter
{
    public const string UnhandledPath = "/test/unhandled";
    public const string NotFoundPath = "/test/not-found";
    public const string DomainRulePath = "/test/domain-rule";

    /// <summary>Stands in for internal details (SQL, connection info) that must never reach a client.</summary>
    public const string SensitiveMessage = "SENSITIVE-INTERNAL-DETAIL-7f3a";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Run(context =>
        {
            if (context.Request.Path == UnhandledPath)
            {
                throw new InvalidOperationException(SensitiveMessage);
            }

            if (context.Request.Path == NotFoundPath)
            {
                throw new NotFoundException("Widget", 42);
            }

            if (context.Request.Path == DomainRulePath)
            {
                throw new DomainException("Widget name must not be empty.");
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });
    };
}
