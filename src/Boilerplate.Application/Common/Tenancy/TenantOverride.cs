namespace Boilerplate.Application.Common.Tenancy;

/// <summary>
/// Sets the tenant explicitly for work that has no tenant claim, such as the bootstrap command and background jobs
/// (ADR-0010, ADR-0022), and for the few requests that act on another tenant: platform administration and accepting
/// an invitation (ADR-0027). Scoped: it applies to the current service scope only.
/// </summary>
public sealed class TenantOverride
{
    public Guid? TenantId { get; private set; }

    public void Set(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant id is required.", nameof(tenantId));
        }

        TenantId = tenantId;
    }
}
