using System.Reflection;
using Boilerplate.Application.Common.Security;
using Boilerplate.Application.Features.Roles;
using Boilerplate.Domain.Authorization;

namespace Boilerplate.UnitTests.Common.Security;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void Catalog_contains_only_canonical_distinct_names()
    {
        PermissionCatalog.All.ShouldAllBe(permission => PermissionName.IsValid(permission));
        PermissionCatalog.All.Distinct(StringComparer.Ordinal).Count().ShouldBe(PermissionCatalog.All.Count);
    }

    // ADR-0022: a feature that declares permission constants must add them to the catalogue,
    // otherwise no role could ever be granted them.
    [Fact]
    public void Every_feature_permission_constant_is_in_the_catalog()
    {
        var declared = typeof(PermissionCatalog).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: true, IsSealed: true }
                && type.Namespace?.StartsWith("Boilerplate.Application.Features", StringComparison.Ordinal) == true
                && type.Name.EndsWith("Permissions", StringComparison.Ordinal))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        declared.ShouldNotBeEmpty();
        declared.ShouldBeSubsetOf(PermissionCatalog.All);
    }

    [Fact]
    public void Role_request_rejects_unknown_permissions()
    {
        var result = new SaveRoleValidator().Validate(new SaveRoleRequest("Editors", ["projects.read", "made.up"]));

        result.Errors.Select(error => error.PropertyName).ShouldBe(["Permissions[1]"]);
    }

    [Fact]
    public void Caller_permissions_hold_explicit_grants_or_everything_for_administrators()
    {
        var editor = new CallerPermissions(false, new HashSet<string>(["projects.read"], StringComparer.Ordinal));
        var administrator = new CallerPermissions(true, new HashSet<string>(StringComparer.Ordinal));

        editor.Holds("projects.read").ShouldBeTrue();
        editor.Holds("projects.manage").ShouldBeFalse();
        administrator.Holds("projects.manage").ShouldBeTrue();
        CallerPermissions.None.Holds("projects.read").ShouldBeFalse();
    }
}
