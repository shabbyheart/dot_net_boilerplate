namespace Boilerplate.Domain.Common;

/// <summary>
/// Data that belongs to exactly one tenant. Persistence filters reads and stamps writes with the
/// current tenant, so domain code never sets <see cref="TenantId"/> (ADR-0010).
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
