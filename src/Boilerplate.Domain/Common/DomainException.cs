namespace Boilerplate.Domain.Common;

/// <summary>
/// A domain rule was violated. The API maps it to a client error once the first endpoint can trigger one (M4, ADR-0006).
/// </summary>
public sealed class DomainException(string message) : Exception(message);
