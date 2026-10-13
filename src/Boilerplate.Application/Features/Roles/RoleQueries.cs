using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Roles;

/// <summary>Query logic shared by several role handlers.</summary>
internal static class RoleQueries
{
    /// <exception cref="NotFoundException">No role with this id exists in the current tenant.</exception>
    public static async Task<Role> GetRoleAsync(this IQueryable<Role> roles, Guid roleId, CancellationToken cancellationToken) =>
        await roles.SingleOrDefaultAsync(role => role.Id == roleId, cancellationToken)
        ?? throw new NotFoundException("Role", roleId);

    /// <summary>Whether another role of the current tenant already uses <paramref name="name"/> (exact match).</summary>
    public static Task<bool> NameIsTakenAsync(
        this IQueryable<Role> roles,
        string name,
        Guid excludingRoleId,
        CancellationToken cancellationToken) =>
        roles.AnyAsync(role => role.Name == name && role.Id != excludingRoleId, cancellationToken);
}
