using Boilerplate.Application.Common.Pagination;

namespace Boilerplate.Application.Features.Roles.ListRoles;

/// <summary>Lists the current tenant's roles, ordered by name.</summary>
public sealed record ListRolesRequest : PageRequest;
