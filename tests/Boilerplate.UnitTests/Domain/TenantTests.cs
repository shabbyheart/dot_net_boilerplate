using Boilerplate.Domain.Common;
using Boilerplate.Domain.Tenants;

namespace Boilerplate.UnitTests.Domain;

public sealed class TenantTests
{
    [Fact]
    public void Create_trims_the_name()
    {
        var tenant = Tenant.Create("  Acme  ");

        tenant.Name.ShouldBe("Acme");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_an_empty_name(string name) =>
        Should.Throw<DomainException>(() => Tenant.Create(name));

    [Fact]
    public void Create_rejects_a_name_longer_than_the_maximum() =>
        Should.Throw<DomainException>(() => Tenant.Create(new string('x', Tenant.NameMaxLength + 1)));

    [Fact]
    public void Create_accepts_a_name_of_maximum_length() =>
        Tenant.Create(new string('x', Tenant.NameMaxLength)).Name.Length.ShouldBe(Tenant.NameMaxLength);

    [Fact]
    public void Rename_applies_the_same_rules()
    {
        var tenant = Tenant.Create("Acme");

        tenant.Rename(" Globex ");

        tenant.Name.ShouldBe("Globex");
        Should.Throw<DomainException>(() => tenant.Rename(" "));
    }
}
