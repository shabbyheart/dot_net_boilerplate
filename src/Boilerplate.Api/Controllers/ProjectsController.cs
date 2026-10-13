using Boilerplate.Api.Authorization;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Features.Projects;
using Boilerplate.Application.Features.Projects.CreateProject;
using Boilerplate.Application.Features.Projects.DeleteProject;
using Boilerplate.Application.Features.Projects.GetProject;
using Boilerplate.Application.Features.Projects.ListProjects;
using Boilerplate.Application.Features.Projects.UpdateProject;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

/// <summary>
/// The current tenant's projects. Reference feature: each action binds the request, calls one handler and
/// returns its result (ADR-0020); validation, errors and tenancy are handled centrally.
/// </summary>
[ApiController]
[Route("api/v1/projects")]
public sealed class ProjectsController : ControllerBase
{
    private const string GetProjectRoute = "GetProject";

    /// <summary>Lists projects ordered by name.</summary>
    [HttpGet]
    [RequirePermission(ProjectPermissions.Read)]
    [ProducesResponseType<PagedResponse<ProjectResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResponse<ProjectResponse>> List(
        [FromQuery] ListProjectsRequest request,
        [FromServices] ListProjectsHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, cancellationToken);

    /// <summary>Gets one project.</summary>
    [HttpGet("{id:guid}", Name = GetProjectRoute)]
    [RequirePermission(ProjectPermissions.Read)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ProjectResponse> Get(
        Guid id,
        [FromServices] GetProjectHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(id, cancellationToken);

    /// <summary>Creates a project.</summary>
    [HttpPost]
    [RequirePermission(ProjectPermissions.Manage)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<CreatedAtRouteResult> Create(
        CreateProjectRequest request,
        [FromServices] CreateProjectHandler handler,
        CancellationToken cancellationToken)
    {
        var project = await handler.HandleAsync(request, cancellationToken);
        return CreatedAtRoute(GetProjectRoute, new { id = project.Id }, project);
    }

    /// <summary>Replaces a project's name and description.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(ProjectPermissions.Manage)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ProjectResponse> Update(
        Guid id,
        UpdateProjectRequest request,
        [FromServices] UpdateProjectHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(id, request, cancellationToken);

    /// <summary>Deletes a project.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(ProjectPermissions.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Delete(
        Guid id,
        [FromServices] DeleteProjectHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return NoContent();
    }
}
