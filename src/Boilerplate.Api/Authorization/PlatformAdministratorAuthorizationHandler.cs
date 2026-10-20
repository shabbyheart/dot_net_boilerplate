using Boilerplate.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace Boilerplate.Api.Authorization;

/// <summary>Evaluates <see cref="PlatformAdministratorRequirement"/> against the database on every request (ADR-0027).</summary>
internal sealed class PlatformAdministratorAuthorizationHandler(
    PlatformAdministratorChecker checker,
    IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<PlatformAdministratorRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformAdministratorRequirement requirement)
    {
        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

        if (await checker.IsPlatformAdministratorAsync(cancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
