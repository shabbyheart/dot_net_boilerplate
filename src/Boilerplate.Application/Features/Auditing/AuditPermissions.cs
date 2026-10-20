namespace Boilerplate.Application.Features.Auditing;

/// <summary>Permissions of the audit log (ADR-0028).</summary>
public static class AuditPermissions
{
    /// <summary>Read the tenant's audit log.</summary>
    public const string Read = "audit.read";
}
