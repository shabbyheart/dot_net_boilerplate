using System.Text.RegularExpressions;
using Boilerplate.Domain.Common;

namespace Boilerplate.Domain.Authorization;

/// <summary>
/// The single canonical form of a permission name: <c>area.action</c> in lowercase, for example
/// <c>roles.manage</c> (ADR-0017). Non-canonical spellings are rejected, never silently converted,
/// so a granted permission and a required permission cannot differ only in spelling.
/// </summary>
public static partial class PermissionName
{
    public const int MaxLength = 100;

    public static bool IsValid(string? permission) =>
        permission is { Length: > 0 and <= MaxLength } && CanonicalForm().IsMatch(permission);

    public static string Validate(string permission) =>
        IsValid(permission)
            ? permission
            : throw new DomainException(
                $"'{permission}' is not a valid permission name. Use lowercase 'area.action' (at most {MaxLength} characters).");

    [GeneratedRegex(@"^[a-z0-9-]+(\.[a-z0-9-]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalForm();
}
