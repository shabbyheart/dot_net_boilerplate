using Boilerplate.Api.Authorization;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Features.Members;
using Boilerplate.Application.Features.Members.GetMember;
using Boilerplate.Application.Features.Members.ListMembers;
using Boilerplate.Application.Features.Members.RemoveMember;
using Boilerplate.Application.Features.Roles;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

/// <summary>
/// Members of the caller's tenant: users who hold at least one role in it (ADR-0029). Membership is role assignments,
/// so it needs the role permissions, and removal passes the escalation guard (ADR-0022).
/// </summary>
[ApiController]
[Route("api/v1/members")]
public sealed class MembersController : ControllerBase
{
    /// <summary>Lists the tenant's members and their roles, ordered by user id.</summary>
    [HttpGet]
    [RequirePermission(RolePermissions.Read)]
    [ProducesResponseType<PagedResponse<MemberResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResponse<MemberResponse>> List(
        [FromQuery] ListMembersRequest request,
        [FromServices] ListMembersHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, cancellationToken);

    /// <summary>Gets one member and their roles.</summary>
    [HttpGet("{userId}")]
    [RequirePermission(RolePermissions.Read)]
    [ProducesResponseType<MemberResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<MemberResponse> Get(string userId, [FromServices] GetMemberHandler handler, CancellationToken cancellationToken) =>
        handler.HandleAsync(userId, cancellationToken);

    /// <summary>Removes a member from the tenant by taking every role they hold away. The last administrator cannot be removed.</summary>
    [HttpDelete("{userId}")]
    [RequirePermission(RolePermissions.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<NoContentResult> Remove(string userId, [FromServices] RemoveMemberHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(userId, cancellationToken);
        return NoContent();
    }
}
