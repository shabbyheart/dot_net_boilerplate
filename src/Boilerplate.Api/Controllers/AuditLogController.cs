using Boilerplate.Api.Authorization;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Features.Auditing;
using Boilerplate.Application.Features.Auditing.ListAuditLog;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

/// <summary>The caller's tenant's audit log (ADR-0028). Read-only: entries cannot be changed or deleted over HTTP.</summary>
[ApiController]
[Route("api/v1/audit-log")]
public sealed class AuditLogController : ControllerBase
{
    /// <summary>Lists audit log entries, newest first, optionally filtered by action, actor and target.</summary>
    [HttpGet]
    [RequirePermission(AuditPermissions.Read)]
    [ProducesResponseType<PagedResponse<AuditLogEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResponse<AuditLogEntryResponse>> List(
        [FromQuery] ListAuditLogRequest request,
        [FromServices] ListAuditLogHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, cancellationToken);
}
