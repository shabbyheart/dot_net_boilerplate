using Boilerplate.Application.Common.Pagination;

namespace Boilerplate.Application.Features.Projects.ListProjects;

/// <summary>Lists the current tenant's projects, ordered by name.</summary>
public sealed record ListProjectsRequest : PageRequest;
