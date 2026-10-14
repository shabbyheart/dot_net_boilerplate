using Boilerplate.Api.Authorization;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Features.Invitations;
using Boilerplate.Application.Features.Tenants;
using Boilerplate.Application.Features.Tenants.CreateTenant;
using Boilerplate.Application.Features.Tenants.GetTenant;
using Boilerplate.Application.Features.Tenants.InviteTenantAdministrator;
using Boilerplate.Application.Features.Tenants.ListTenants;
using Boilerplate.Application.Features.Tenants.RenameTenant;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

/// <summary>
/// Tenant administration across the platform (ADR-0027). Platform administrators only; their token need not name a
/// tenant. Platform administrators get no access to data inside tenants: they invite tenant administrators instead.
/// </summary>
[ApiController]
[Route("api/v1/platform/tenants")]
[RequirePlatformAdministrator]
public sealed class PlatformTenantsController : ControllerBase
{
    private const string GetTenantRoute = "GetTenant";

    /// <summary>Lists tenants ordered by name.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<TenantResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResponse<TenantResponse>> List(
        [FromQuery] ListTenantsRequest request,
        [FromServices] ListTenantsHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, cancellationToken);

    /// <summary>Gets one tenant.</summary>
    [HttpGet("{id:guid}", Name = GetTenantRoute)]
    [ProducesResponseType<TenantResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<TenantResponse> Get(Guid id, [FromServices] GetTenantHandler handler, CancellationToken cancellationToken) =>
        handler.HandleAsync(id, cancellationToken);

    /// <summary>Creates a tenant with its administrator role and e-mails an invitation to its first administrator.</summary>
    [HttpPost]
    [ProducesResponseType<CreateTenantResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<CreatedAtRouteResult> Create(
        CreateTenantRequest request,
        [FromServices] CreateTenantHandler handler,
        CancellationToken cancellationToken)
    {
        var created = await handler.HandleAsync(request, cancellationToken);
        return CreatedAtRoute(GetTenantRoute, new { id = created.Tenant.Id }, created);
    }

    /// <summary>Renames a tenant.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TenantResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<TenantResponse> Rename(
        Guid id,
        RenameTenantRequest request,
        [FromServices] RenameTenantHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(id, request, cancellationToken);

    /// <summary>E-mails an invitation to become an administrator of the tenant.</summary>
    [HttpPost("{id:guid}/administrator-invitations")]
    [ProducesResponseType<InvitationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ObjectResult> InviteAdministrator(
        Guid id,
        InviteTenantAdministratorRequest request,
        [FromServices] InviteTenantAdministratorHandler handler,
        CancellationToken cancellationToken)
    {
        // No Location: the invitation belongs to the tenant, and only its members can read it.
        var invitation = await handler.HandleAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, invitation);
    }
}
