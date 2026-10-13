namespace Boilerplate.Application.Common.Exceptions;

/// <summary>
/// The caller is authenticated and may use the endpoint, but not for this particular change, for example granting a
/// permission they do not hold (ADR-0022). Translated to a 403 ProblemDetails response by the API (ADR-0006).
/// </summary>
public sealed class ForbiddenException(string message) : Exception(message);
