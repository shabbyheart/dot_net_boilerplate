namespace Boilerplate.Domain.Common;

/// <summary>
/// Entities whose creation and last update are recorded: when, and by which user.
/// The values are set by persistence on save, never by domain code (ADR-0012, ADR-0016).
/// </summary>
public interface IAuditable
{
    /// <summary>Maximum length of a stored user id (the token's <c>sub</c> claim).</summary>
    const int UserIdMaxLength = 200;

    DateTimeOffset CreatedAt { get; }

    /// <summary>The user who created the entity, or <see langword="null"/> for system operations.</summary>
    string? CreatedBy { get; }

    DateTimeOffset UpdatedAt { get; }

    /// <summary>The user who last updated the entity, or <see langword="null"/> for system operations.</summary>
    string? UpdatedBy { get; }
}
