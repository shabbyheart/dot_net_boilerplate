using Boilerplate.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace Boilerplate.Api.Authorization;

/// <summary>Evaluates <see cref="PermissionRequirement"/> against the database on every request (ADR-0017).</summary>
internal sealed class PermissionAuthorizationHandler(
    PermissionChecker permissionChecker,
    IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

        if (await permissionChecker.HasPermissionAsync(requirement.Permission, cancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
