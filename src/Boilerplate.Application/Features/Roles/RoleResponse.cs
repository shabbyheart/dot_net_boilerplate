using System.Linq.Expressions;
using Boilerplate.Domain.Authorization;

namespace Boilerplate.Application.Features.Roles;

/// <summary>A role as returned by the API.</summary>
/// <param name="IsAdministrator">The built-in role that grants every permission; its <paramref name="Permissions"/> list is empty.</param>
/// <param name="Permissions">Explicitly granted permissions, sorted.</param>
public sealed record RoleResponse(
    Guid Id,
    string Name,
    bool IsAdministrator,
    IReadOnlyList<string> Permissions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Hand-written mapping (ADR-0015), translated to SQL in queries.</summary>
    public static readonly Expression<Func<Role, RoleResponse>> FromRole = role => new RoleResponse(
        role.Id,
        role.Name,
        role.IsAdministrator,
        role.Permissions.Select(granted => granted.Name).OrderBy(name => name).ToList(),
        role.CreatedAt,
        role.UpdatedAt);

    private static readonly Func<Role, RoleResponse> MapRole = FromRole.Compile();

    public static RoleResponse From(Role role) => MapRole(role);
}
