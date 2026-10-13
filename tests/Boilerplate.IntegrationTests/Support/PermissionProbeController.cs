using Boilerplate.Api.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// A test-only endpoint protected by a permission, so authorization can be tested before any production
/// feature declares one (ADR-0017). Registered by <see cref="ApiFactory"/>.
/// </summary>
[ApiController]
[Route("api/v1/test/permission-probe")]
public sealed class PermissionProbeController : ControllerBase
{
    public const string Path = "/api/v1/test/permission-probe";
    public const string Permission = "probe.read";

    [HttpGet]
    [RequirePermission(Permission)]
    public IActionResult Get() => Ok();
}
