using Boilerplate.Application.Common.Tenancy;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>A tenant context the test sets explicitly, one per service scope.</summary>
internal sealed class TestTenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
}
