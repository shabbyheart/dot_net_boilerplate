using Boilerplate.Application.Common.Tenancy;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// A tenant context the test sets explicitly, one per service scope. Like the real implementation it prefers
/// <see cref="TenantOverride"/>, which the outbox dispatcher uses (ADR-0022).
/// </summary>
internal sealed class TestTenantContext(TenantOverride tenantOverride) : ITenantContext
{
    public Guid? ExplicitTenantId { get; set; }

    public Guid? TenantId => tenantOverride.TenantId ?? ExplicitTenantId;
}
