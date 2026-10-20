using Boilerplate.Application.Common.Auditing;
using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Application.Features.Invitations;
using Boilerplate.Application.Features.Roles;
using Boilerplate.Domain.Authorization;

namespace Boilerplate.Application.Features.Tenants.InviteTenantAdministrator;

/// <summary>
/// Invites an administrator to an existing tenant, for example when the first invitation expired or every
/// administrator left (ADR-0027). Creates the administrator role if the tenant has none.
/// </summary>
public sealed class InviteTenantAdministratorHandler(
    IAppDbContext db,
    TenantOverride tenantOverride,
    InvitationSender sender,
    AuditLog auditLog)
{
    /// <exception cref="NotFoundException">No tenant with this id exists.</exception>
    /// <exception cref="ConflictException">Invitations are not configured.</exception>
    public async Task<InvitationResponse> HandleAsync(
        Guid tenantId,
        InviteTenantAdministratorRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.GetTenantAsync(tenantId, cancellationToken);

        // The role and the invitation belong to that tenant, not to the caller's.
        tenantOverride.Set(tenant.Id);
        var administrators = await db.Roles.FindAdministratorsAsync(cancellationToken);
        if (administrators is null)
        {
            administrators = Role.CreateAdministrators();
            db.Roles.Add(administrators);
            auditLog.Record(AuditActions.RoleCreated, administrators.Id, RoleAuditing.Created(administrators));
        }

        var invitation = await sender.IssueAsync(administrators, request.Email, tenant.Name, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return InvitationResponse.From(invitation);
    }
}
