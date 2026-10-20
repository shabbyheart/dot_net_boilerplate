namespace Boilerplate.Application.Common.Exceptions;

/// <summary>
/// The password was correct, but the account needs a second factor that the sign-in did not include (ADR-0031).
/// Translated to a 401 ProblemDetails response with <c>"twoFactorRequired": true</c>, so clients know to ask for a code.
/// </summary>
public sealed class TwoFactorRequiredException(string message) : Exception(message);
