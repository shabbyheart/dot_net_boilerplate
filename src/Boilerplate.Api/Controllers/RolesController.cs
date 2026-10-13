using Boilerplate.Api.Authorization;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Features.Roles;
using Boilerplate.Application.Features.Roles.AssignRole;
using Boilerplate.Application.Features.Roles.CreateRole;
using Boilerplate.Application.Features.Roles.DeleteRole;
using Boilerplate.Application.Features.Roles.GetRole;
using Boilerplate.Application.Features.Roles.ListRoleAssignments;
using Boilerplate.Application.Features.Roles.ListRoles;
using Boilerplate.Application.Features.Roles.UnassignRole;
using Boilerplate.Application.Features.Roles.UpdateRole;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

/// <summary>
/// Role administration within the caller's tenant (ADR-0022). Callers can only grant, remove or hand out
/// permissions they hold themselves.
/// </summary>
[ApiController]
[Route("api/v1/roles")]
public sealed class RolesController : ControllerBase
{
    private const string GetRoleRoute = "GetRole";

    /// <summary>Lists roles ordered by name.</summary>
    [HttpGet]
    [RequirePermission(RolePermissions.Read)]
    [ProducesResponseType<PagedResponse<RoleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResponse<RoleResponse>> List(
        [FromQuery] ListRolesRequest request,
        [FromServices] ListRolesHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, cancellationToken);

    /// <summary>Gets one role.</summary>
    [HttpGet("{id:guid}", Name = GetRoleRoute)]
    [RequirePermission(RolePermissions.Read)]
    [ProducesResponseType<RoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<RoleResponse> Get(Guid id, [FromServices] GetRoleHandler handler, CancellationToken cancellationToken) =>
        handler.HandleAsync(id, cancellationToken);

    /// <summary>Creates a role with the given permissions.</summary>
    [HttpPost]
    [RequirePermission(RolePermissions.Manage)]
    [ProducesResponseType<RoleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<CreatedAtRouteResult> Create(
        SaveRoleRequest request,
        [FromServices] CreateRoleHandler handler,
        CancellationToken cancellationToken)
    {
        var role = await handler.HandleAsync(request, cancellationToken);
        return CreatedAtRoute(GetRoleRoute, new { id = role.Id }, role);
    }

    /// <summary>Replaces a role's name and permissions. The administrator role cannot be changed.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(RolePermissions.Manage)]
    [ProducesResponseType<RoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<RoleResponse> Update(
        Guid id,
        SaveRoleRequest request,
        [FromServices] UpdateRoleHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(id, request, cancellationToken);

    /// <summary>Deletes a role and its assignments. The administrator role cannot be deleted.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(RolePermissions.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Delete(Guid id, [FromServices] DeleteRoleHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Lists the users who hold the role, ordered by user id.</summary>
    [HttpGet("{id:guid}/assignments")]
    [RequirePermission(RolePermissions.Read)]
    [ProducesResponseType<PagedResponse<RoleAssignmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<PagedResponse<RoleAssignmentResponse>> ListAssignments(
        Guid id,
        [FromQuery] ListRoleAssignmentsRequest request,
        [FromServices] ListRoleAssignmentsHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(id, request, cancellationToken);

    /// <summary>Gives the role to a user (identified by token subject). Idempotent.</summary>
    [HttpPut("{id:guid}/assignments/{userId}")]
    [RequirePermission(RolePermissions.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Assign(
        Guid id,
        string userId,
        [FromServices] AssignRoleHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, userId, cancellationToken);
        return NoContent();
    }

    /// <summary>Takes the role away from a user. The last administrator cannot be removed.</summary>
    [HttpDelete("{id:guid}/assignments/{userId}")]
    [RequirePermission(RolePermissions.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<NoContentResult> Unassign(
        Guid id,
        string userId,
        [FromServices] UnassignRoleHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, userId, cancellationToken);
        return NoContent();
    }
}
