namespace Boilerplate.Application.Common.Exceptions;

/// <summary>
/// Credentials or a refresh token were not accepted (ADR-0025). Translated to a 401 ProblemDetails response by the
/// API (ADR-0006). The message never says which part was wrong.
/// </summary>
public sealed class AuthenticationFailedException(string message) : Exception(message);
