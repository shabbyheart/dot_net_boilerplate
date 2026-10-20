using Boilerplate.Application.Common.Persistence;
using Boilerplate.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Members;

/// <summary>Query logic shared by the member handlers. Members are derived from role assignments (ADR-0029).</summary>
internal static class MemberQueries
{
    /// <summary>One row per member: the user and their earliest current assignment, ordered by user id.</summary>
    public static IQueryable<MemberSummary> Summaries(this IQueryable<RoleAssignment> assignments) =>
        assignments
            .GroupBy(assignment => assignment.UserId)
            .OrderBy(group => group.Key)
            .Select(group => new MemberSummary(group.Key, group.Min(assignment => assignment.CreatedAt)));

    /// <summary>Adds the roles of each member, with one query for all of them.</summary>
    public static async Task<IReadOnlyList<MemberResponse>> WithRolesAsync(
        this IAppDbContext db,
        IReadOnlyList<MemberSummary> members,
        CancellationToken cancellationToken)
    {
        var userIds = members.Select(member => member.UserId).ToList();
        var held = await db.RoleAssignments
            .AsNoTracking()
            .Where(assignment => userIds.Contains(assignment.UserId))
            .Join(
                db.Roles,
                assignment => assignment.RoleId,
                role => role.Id,
                (assignment, role) => new { assignment.UserId, Role = new MemberRoleResponse(role.Id, role.Name, role.IsAdministrator) })
            .ToListAsync(cancellationToken);

        var rolesByUser = held.ToLookup(row => row.UserId, row => row.Role, StringComparer.Ordinal);
        return members
            .Select(member => new MemberResponse(
                member.UserId,
                member.JoinedAt,
                rolesByUser[member.UserId].OrderBy(role => role.Name, StringComparer.Ordinal).ToList()))
            .ToList();
    }
}
