using Boilerplate.Application.Common.Auditing;
using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Application.Features.Invitations;
using Boilerplate.Application.Features.Roles;
using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Tenants;

namespace Boilerplate.Application.Features.Tenants.CreateTenant;

/// <summary>
/// Creates a tenant with its administrator role and invites its first administrator, in one transaction (ADR-0027).
/// </summary>
public sealed class CreateTenantHandler(
    IAppDbContext db,
    TenantOverride tenantOverride,
    InvitationSender sender,
    AuditLog auditLog)
{
    /// <exception cref="ConflictException">The name is taken, or invitations are not configured.</exception>
    public async Task<CreateTenantResponse> HandleAsync(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        // The entity normalizes the name, so check uniqueness against the normalized value.
        var tenant = Tenant.Create(request.Name);
        if (await db.Tenants.NameIsTakenAsync(tenant.Name, tenant.Id, cancellationToken))
        {
            throw new ConflictException($"A tenant named '{tenant.Name}' already exists.");
        }

        db.Tenants.Add(tenant);

        // The administrator role, the invitation and the audit log entries belong to the new tenant, not to the caller's.
        tenantOverride.Set(tenant.Id);
        auditLog.Record(AuditActions.TenantCreated, tenant.Id, $"Created tenant '{tenant.Name}'.");
        var administrators = Role.CreateAdministrators();
        db.Roles.Add(administrators);
        auditLog.Record(AuditActions.RoleCreated, administrators.Id, RoleAuditing.Created(administrators));

        var invitation = await sender.IssueAsync(administrators, request.AdministratorEmail, tenant.Name, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateTenantResponse(TenantResponse.From(tenant), InvitationResponse.From(invitation));
    }
}
