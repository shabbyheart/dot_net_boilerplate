using Boilerplate.Api.Authorization;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Features.Invitations;
using Boilerplate.Application.Features.Invitations.AcceptInvitation;
using Boilerplate.Application.Features.Invitations.CreateInvitation;
using Boilerplate.Application.Features.Invitations.GetInvitation;
using Boilerplate.Application.Features.Invitations.ListInvitations;
using Boilerplate.Application.Features.Invitations.RevokeInvitation;
using Boilerplate.Application.Features.Roles;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

/// <summary>
/// Invitations to roles of the caller's tenant (ADR-0027). Inviting is handing out a role, so it needs the same
/// permissions and passes the same escalation guard as assigning one (ADR-0022).
/// </summary>
[ApiController]
[Route("api/v1/invitations")]
public sealed class InvitationsController : ControllerBase
{
    private const string GetInvitationRoute = "GetInvitation";

    /// <summary>Lists the tenant's invitations, newest first.</summary>
    [HttpGet]
    [RequirePermission(RolePermissions.Read)]
    [ProducesResponseType<PagedResponse<InvitationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResponse<InvitationResponse>> List(
        [FromQuery] ListInvitationsRequest request,
        [FromServices] ListInvitationsHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, cancellationToken);

    /// <summary>Gets one invitation.</summary>
    [HttpGet("{id:guid}", Name = GetInvitationRoute)]
    [RequirePermission(RolePermissions.Read)]
    [ProducesResponseType<InvitationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<InvitationResponse> Get(Guid id, [FromServices] GetInvitationHandler handler, CancellationToken cancellationToken) =>
        handler.HandleAsync(id, cancellationToken);

    /// <summary>E-mails an invitation to a role. Replaces earlier pending invitations of the address to the role.</summary>
    [HttpPost]
    [RequirePermission(RolePermissions.Manage)]
    [ProducesResponseType<InvitationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<CreatedAtRouteResult> Create(
        CreateInvitationRequest request,
        [FromServices] CreateInvitationHandler handler,
        CancellationToken cancellationToken)
    {
        var invitation = await handler.HandleAsync(request, cancellationToken);
        return CreatedAtRoute(GetInvitationRoute, new { id = invitation.Id }, invitation);
    }

    /// <summary>Revokes a pending invitation. Revoking an accepted or revoked invitation changes nothing.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(RolePermissions.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Revoke(Guid id, [FromServices] RevokeInvitationHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Accepts an invitation with the token from its e-mail: the signed-in caller receives the role. Any signed-in user
    /// may call this; the token is the proof. The caller's token need not name a tenant.
    /// </summary>
    [HttpPost("accept")]
    [ProducesResponseType<AcceptInvitationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<AcceptInvitationResponse> Accept(
        AcceptInvitationRequest request,
        [FromServices] AcceptInvitationHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, cancellationToken);
}
