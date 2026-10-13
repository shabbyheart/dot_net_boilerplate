using Boilerplate.Domain.Tenants;

namespace Boilerplate.UnitTests.Domain;

public sealed class EntityTests
{
    // ADR-0012: ids are GUID v7, created by the domain without a database round trip.
    [Fact]
    public void New_entities_get_distinct_version_7_ids()
    {
        var first = Tenant.Create("first");
        var second = Tenant.Create("second");

        first.Id.Version.ShouldBe(7);
        second.Id.ShouldNotBe(first.Id);
    }
}
