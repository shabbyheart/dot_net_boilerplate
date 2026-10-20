using Boilerplate.Domain.Authorization;

namespace Boilerplate.Application.Features.Roles;

/// <summary>Wording of the audit log entries for role changes (ADR-0028).</summary>
internal static class RoleAuditing
{
    public static string Created(Role role) =>
        role.IsAdministrator
            ? $"Created role '{role.Name}' with every permission."
            : $"Created role '{role.Name}' with permissions: {List(role.Permissions.Select(granted => granted.Name))}.";

    public static string Updated(string previousName, string name, IReadOnlySet<string> before, IReadOnlySet<string> after)
    {
        var changes = new List<string>();
        if (!string.Equals(name, previousName, StringComparison.Ordinal))
        {
            changes.Add($"renamed from '{previousName}'");
        }

        var added = after.Where(permission => !before.Contains(permission)).ToList();
        if (added.Count > 0)
        {
            changes.Add($"added {List(added)}");
        }

        var removed = before.Where(permission => !after.Contains(permission)).ToList();
        if (removed.Count > 0)
        {
            changes.Add($"removed {List(removed)}");
        }

        return $"Changed role '{name}': {string.Join("; ", changes)}.";
    }

    /// <summary>Permission names, sorted, or "none".</summary>
    private static string List(IEnumerable<string> permissions)
    {
        var sorted = permissions.Order(StringComparer.Ordinal).ToList();
        return sorted.Count == 0 ? "none" : string.Join(", ", sorted);
    }
}
