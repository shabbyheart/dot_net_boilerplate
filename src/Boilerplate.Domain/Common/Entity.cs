namespace Boilerplate.Domain.Common;

/// <summary>
/// Base class for entities: identity by <see cref="Id"/>, a time-ordered GUID v7 assigned at creation (ADR-0012).
/// </summary>
public abstract class Entity
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
}
