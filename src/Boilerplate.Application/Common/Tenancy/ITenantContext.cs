namespace Boilerplate.Application.Common.Tenancy;

/// <summary>
/// The tenant the current operation acts for (ADR-0010). <see langword="null"/> when there is none,
/// in which case tenant-owned data is neither readable nor writable.
/// Implementations must return <see cref="TenantOverride.TenantId"/> when it is set (ADR-0022).
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
}
