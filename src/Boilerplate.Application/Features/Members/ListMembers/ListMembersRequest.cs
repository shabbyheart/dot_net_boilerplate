using Boilerplate.Application.Common.Pagination;

namespace Boilerplate.Application.Features.Members.ListMembers;

/// <summary>Lists the current tenant's members, ordered by user id.</summary>
public sealed record ListMembersRequest : PageRequest;
