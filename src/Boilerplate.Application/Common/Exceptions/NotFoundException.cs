namespace Boilerplate.Application.Common.Exceptions;

/// <summary>
/// The requested resource does not exist (or is not visible to the caller).
/// Translated to a 404 ProblemDetails response by the API (ADR-0006).
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} '{key}' was not found.")
    {
        ResourceName = resourceName;
        Key = key;
    }

    public string ResourceName { get; }

    public object Key { get; }
}
