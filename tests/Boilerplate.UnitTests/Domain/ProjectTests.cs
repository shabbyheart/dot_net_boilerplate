using Boilerplate.Domain.Common;
using Boilerplate.Domain.Projects;

namespace Boilerplate.UnitTests.Domain;

public sealed class ProjectTests
{
    [Fact]
    public void Create_trims_name_and_description()
    {
        var project = Project.Create("  Website  ", "  Public site  ");

        project.Name.ShouldBe("Website");
        project.Description.ShouldBe("Public site");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_description_means_no_description(string? description) =>
        Project.Create("Website", description).Description.ShouldBeNull();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_must_not_be_empty(string name) =>
        Should.Throw<DomainException>(() => Project.Create(name, null));

    [Fact]
    public void Name_longer_than_the_maximum_is_rejected() =>
        Should.Throw<DomainException>(() => Project.Create(new string('n', Project.NameMaxLength + 1), null));

    [Fact]
    public void Description_longer_than_the_maximum_is_rejected() =>
        Should.Throw<DomainException>(() => Project.Create("Website", new string('d', Project.DescriptionMaxLength + 1)));

    [Fact]
    public void Update_applies_the_same_rules()
    {
        var project = Project.Create("Website", "Old");

        project.Update(" Shop ", " ");

        project.Name.ShouldBe("Shop");
        project.Description.ShouldBeNull();
        Should.Throw<DomainException>(() => project.Update(" ", null));
    }
}
