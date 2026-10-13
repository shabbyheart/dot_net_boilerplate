using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace Boilerplate.Api.ErrorHandling;

/// <summary>
/// Translates exceptions into RFC 9457 ProblemDetails responses (ADR-0006).
/// Unexpected exceptions become a generic 500: their type and message never reach the client.
/// </summary>
internal sealed partial class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, detail) = exception switch
        {
            NotFoundException notFound => (StatusCodes.Status404NotFound, notFound.Message),
            ConflictException conflict => (StatusCodes.Status409Conflict, conflict.Message),
            ForbiddenException forbidden => (StatusCodes.Status403Forbidden, forbidden.Message),

            // A request that passed validation but breaks a domain rule; the message is written for callers.
            DomainException domainRule => (StatusCodes.Status400BadRequest, domainRule.Message),

            // Malformed or oversized requests detected by the server (for example 413 above the body size limit).
            BadHttpRequestException badRequest => (badRequest.StatusCode, badRequest.Message),
            _ => (StatusCodes.Status500InternalServerError, null),
        };

        // Since .NET 10 the exception handler middleware does not log exceptions that a handler handles.
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(exception, httpContext.Request.Method, httpContext.Request.Path);
        }
        else if (logger.IsEnabled(LogLevel.Information))
        {
            var exceptionType = exception.GetType().Name;
            LogHandledException(exceptionType, statusCode, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = statusCode, Detail = detail },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing {Method} {Path}")]
    private partial void LogUnhandledException(Exception exception, string method, PathString path);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ExceptionType} translated to status {StatusCode} for {Method} {Path}")]
    private partial void LogHandledException(string exceptionType, int statusCode, string method, PathString path);
}
