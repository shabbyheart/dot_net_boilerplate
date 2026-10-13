using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Common;

namespace Boilerplate.UnitTests.Domain;

/// <summary>The built-in administrator role (ADR-0022).</summary>
public sealed class AdministratorRoleTests
{
    [Fact]
    public void Grants_every_permission_implicitly_and_stores_none()
    {
        var administrators = Role.CreateAdministrators();

        administrators.IsAdministrator.ShouldBeTrue();
        administrators.Name.ShouldBe(Role.AdministratorsName);
        administrators.HasPermission("any.permission-added-later").ShouldBeTrue();
        administrators.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public void Cannot_be_changed()
    {
        var administrators = Role.CreateAdministrators();

        Should.Throw<DomainException>(() => administrators.Rename("Admins"));
        Should.Throw<DomainException>(() => administrators.Grant("projects.read"));
        Should.Throw<DomainException>(() => administrators.Revoke("projects.read"));
        Should.Throw<DomainException>(() => administrators.ReplacePermissions(["projects.read"]));
    }

    [Fact]
    public void Cannot_be_deleted() =>
        Should.Throw<DomainException>(() => Role.CreateAdministrators().EnsureDeletable());

    [Fact]
    public void Ordinary_roles_can_be_deleted_and_grant_only_their_permissions()
    {
        var role = Role.Create("Editors");
        role.Grant("projects.read");

        role.EnsureDeletable();
        role.IsAdministrator.ShouldBeFalse();
        role.HasPermission("projects.read").ShouldBeTrue();
        role.HasPermission("projects.manage").ShouldBeFalse();
    }

    [Fact]
    public void Replace_permissions_sets_the_exact_set()
    {
        var role = Role.Create("Editors");
        role.Grant("projects.read");
        role.Grant("roles.read");

        role.ReplacePermissions(["projects.read", "projects.manage", "projects.manage"]);

        role.Permissions.Select(granted => granted.Name).ShouldBe(["projects.read", "projects.manage"], ignoreOrder: true);
    }
}
