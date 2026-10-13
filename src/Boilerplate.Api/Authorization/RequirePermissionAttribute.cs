using Boilerplate.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Boilerplate.Api.Authorization;

/// <summary>
/// Requires the caller to hold a permission in their current tenant, for example
/// <c>[RequirePermission("roles.manage")]</c> (ADR-0017). Without it the request gets 403;
/// without a valid token, 401.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public RequirePermissionAttribute(string permission)
    {
        // An invalid name could never be granted, so fail when the endpoint is first evaluated.
        Permission = PermissionName.Validate(permission);
    }

    public string Permission { get; }

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new PermissionRequirement(Permission);
    }
}
