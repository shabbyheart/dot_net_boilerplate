namespace Boilerplate.Application.Features.Members;

/// <summary>A member of the current tenant: a user who holds at least one role in it (ADR-0029).</summary>
/// <param name="UserId">The user's token subject.</param>
/// <param name="JoinedAt">When the user received their first role that they still hold.</param>
/// <param name="Roles">The roles the user holds, ordered by name.</param>
public sealed record MemberResponse(string UserId, DateTimeOffset JoinedAt, IReadOnlyList<MemberRoleResponse> Roles);
