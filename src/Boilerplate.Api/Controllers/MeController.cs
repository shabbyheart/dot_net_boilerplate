using Boilerplate.Application.Common.Security;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Application.Features.Members.LeaveTenant;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

/// <summary>Who the caller is, as resolved from their token (ADR-0016, ADR-0010).</summary>
[ApiController]
[Route("api/v1/me")]
public sealed class MeController(ICurrentUser currentUser, ITenantContext tenantContext) : ControllerBase
{
    /// <summary>The caller's user id and tenant id. The tenant is <see langword="null"/> when the token has none.</summary>
    [HttpGet]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<MeResponse> Get()
    {
        // The fallback policy guarantees an authenticated user with a subject here.
        return new MeResponse(currentUser.UserId!, tenantContext.TenantId);
    }

    /// <summary>
    /// Leaves the token's tenant: the caller gives up every role they hold in it (ADR-0029). The last administrator
    /// cannot leave.
    /// </summary>
    [HttpDelete("membership")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<NoContentResult> LeaveTenant([FromServices] LeaveTenantHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(cancellationToken);
        return NoContent();
    }
}
