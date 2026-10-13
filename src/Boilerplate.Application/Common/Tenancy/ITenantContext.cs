namespace Boilerplate.Application.Common.Tenancy;

/// <summary>
/// The tenant the current operation acts for (ADR-0010). <see langword="null"/> when there is none,
/// in which case tenant-owned data is neither readable nor writable.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
}
