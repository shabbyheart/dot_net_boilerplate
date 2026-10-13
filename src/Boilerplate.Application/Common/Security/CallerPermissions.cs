namespace Boilerplate.Application.Common.Security;

/// <summary>What the current user may do in the current tenant, used by escalation guards (ADR-0022).</summary>
public sealed record CallerPermissions(bool IsAdministrator, IReadOnlySet<string> Permissions)
{
    public static CallerPermissions None { get; } = new(false, new HashSet<string>(StringComparer.Ordinal));

    public bool Holds(string permission) => IsAdministrator || Permissions.Contains(permission);
}
