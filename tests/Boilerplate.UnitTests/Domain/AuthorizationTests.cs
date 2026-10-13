using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Common;

namespace Boilerplate.UnitTests.Domain;

public sealed class AuthorizationTests
{
    [Theory]
    [InlineData("roles.manage")]
    [InlineData("projects.read")]
    [InlineData("billing.invoices.export")]
    [InlineData("api-keys.rotate")]
    public void Canonical_permission_names_are_valid(string permission) =>
        PermissionName.IsValid(permission).ShouldBeTrue();

    // ADR-0017: only one spelling exists, so a grant and a requirement cannot differ by case or format.
    [Theory]
    [InlineData("Roles.Manage")]
    [InlineData("roles")]
    [InlineData("roles.")]
    [InlineData(".manage")]
    [InlineData("roles manage")]
    [InlineData("roles..manage")]
    [InlineData("")]
    public void Non_canonical_permission_names_are_invalid(string permission) =>
        PermissionName.IsValid(permission).ShouldBeFalse();

    [Fact]
    public void Permission_names_longer_than_the_maximum_are_invalid() =>
        PermissionName.IsValid("a." + new string('b', PermissionName.MaxLength - 1)).ShouldBeFalse();

    [Fact]
    public void Grant_adds_a_permission_once()
    {
        var role = Role.Create("Editors");

        role.Grant("projects.edit");
        role.Grant("projects.edit");

        role.Permissions.Select(permission => permission.Name).ShouldBe(["projects.edit"]);
        role.HasPermission("projects.edit").ShouldBeTrue();
    }

    [Fact]
    public void Grant_rejects_a_non_canonical_name()
    {
        var role = Role.Create("Editors");

        Should.Throw<DomainException>(() => role.Grant("Projects.Edit"));
        role.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public void Revoke_removes_the_permission()
    {
        var role = Role.Create("Editors");
        role.Grant("projects.edit");
        role.Grant("projects.read");

        role.Revoke("projects.edit");

        role.HasPermission("projects.edit").ShouldBeFalse();
        role.HasPermission("projects.read").ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Role_name_must_not_be_empty(string name) =>
        Should.Throw<DomainException>(() => Role.Create(name));

    [Fact]
    public void Role_name_is_trimmed() =>
        Role.Create("  Editors ").Name.ShouldBe("Editors");

    [Fact]
    public void Assignment_links_a_user_to_a_role()
    {
        var role = Role.Create("Editors");

        var assignment = RoleAssignment.Create("user-1", role);

        assignment.UserId.ShouldBe("user-1");
        assignment.RoleId.ShouldBe(role.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Assignment_needs_a_user_id(string userId) =>
        Should.Throw<DomainException>(() => RoleAssignment.Create(userId, Role.Create("Editors")));

    [Fact]
    public void Assignment_rejects_a_user_id_longer_than_the_maximum() =>
        Should.Throw<DomainException>(() =>
            RoleAssignment.Create(new string('u', IAuditable.UserIdMaxLength + 1), Role.Create("Editors")));
}
