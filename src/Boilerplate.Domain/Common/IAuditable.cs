namespace Boilerplate.Domain.Common;

/// <summary>
/// Entities whose creation and last-update times are recorded.
/// The values are set by persistence on save, never by domain code (ADR-0012).
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }

    DateTimeOffset UpdatedAt { get; }
}
