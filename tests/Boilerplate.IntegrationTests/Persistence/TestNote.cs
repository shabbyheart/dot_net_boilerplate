using Boilerplate.Domain.Common;

namespace Boilerplate.IntegrationTests.Persistence;

/// <summary>
/// A tenant-owned probe entity that exists only in tests, so the tenancy rules can be verified
/// before the first real tenant-owned feature exists.
/// </summary>
internal sealed class TestNote : Entity, ITenantOwned
{
    public const int TitleMaxLength = 100;

    public TestNote(string title)
    {
        Title = title;
    }

    public string Title { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Simulates code that tries to choose the tenant itself.</summary>
    public static TestNote ForTenant(string title, Guid tenantId) => new(title) { TenantId = tenantId };

    public void Rename(string title) => Title = title;
}
