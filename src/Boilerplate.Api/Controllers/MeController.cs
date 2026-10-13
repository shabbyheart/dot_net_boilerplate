using Boilerplate.Application.Common.Security;
using Boilerplate.Application.Common.Tenancy;
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
}
