namespace Boilerplate.Application.Common.Exceptions;

/// <summary>
/// The request conflicts with the current state, for example a duplicate unique value.
/// Translated to a 409 ProblemDetails response by the API (ADR-0006).
/// </summary>
public sealed class ConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);
