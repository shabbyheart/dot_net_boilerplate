using System.Linq.Expressions;
using Boilerplate.Domain.Authorization;

namespace Boilerplate.Application.Features.Roles;

/// <summary>A user who holds a role.</summary>
/// <param name="UserId">The user's token subject.</param>
/// <param name="AssignedAt">When the role was assigned.</param>
/// <param name="AssignedBy">Who assigned it, or <see langword="null"/> for the bootstrap command.</param>
public sealed record RoleAssignmentResponse(string UserId, DateTimeOffset AssignedAt, string? AssignedBy)
{
    public static readonly Expression<Func<RoleAssignment, RoleAssignmentResponse>> FromAssignment =
        assignment => new RoleAssignmentResponse(assignment.UserId, assignment.CreatedAt, assignment.CreatedBy);
}
