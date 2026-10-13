using Boilerplate.Application.Common.Pagination;

namespace Boilerplate.Application.Features.Roles.ListRoleAssignments;

/// <summary>Lists the users who hold a role, ordered by user id.</summary>
public sealed record ListRoleAssignmentsRequest : PageRequest;
