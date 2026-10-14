using Microsoft.AspNetCore.Authorization;

namespace Boilerplate.Api.Authorization;

/// <summary>
/// Requires the caller to be a platform administrator (ADR-0027), whatever tenant their token names, if any.
/// Without the grant the request gets 403; without a valid token, 401.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequirePlatformAdministratorAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new PlatformAdministratorRequirement();
    }
}

/// <summary>The caller must be a platform administrator (ADR-0027).</summary>
public sealed record PlatformAdministratorRequirement : IAuthorizationRequirement;
