namespace Boilerplate.Application.Common.Auditing;

/// <summary>The recorded actions (ADR-0028). Clients filter on these names: never rename them.</summary>
public static class AuditActions
{
    public const string RoleCreated = "role.created";
    public const string RoleUpdated = "role.updated";
    public const string RoleDeleted = "role.deleted";
    public const string RoleAssigned = "role.assigned";
    public const string RoleUnassigned = "role.unassigned";

    public const string InvitationCreated = "invitation.created";
    public const string InvitationRevoked = "invitation.revoked";
    public const string InvitationAccepted = "invitation.accepted";

    public const string TenantCreated = "tenant.created";
    public const string TenantRenamed = "tenant.renamed";
}
