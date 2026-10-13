using Boilerplate.Api.Authorization;
using Boilerplate.Application.Common.Security;
using Boilerplate.Application.Features.Roles;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

/// <summary>The permission catalogue, for role administration clients (ADR-0022).</summary>
[ApiController]
[Route("api/v1/permissions")]
public sealed class PermissionsController : ControllerBase
{
    /// <summary>Every permission a role can grant.</summary>
    [HttpGet]
    [RequirePermission(RolePermissions.Read)]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public IReadOnlyList<string> List() => PermissionCatalog.All;
}
