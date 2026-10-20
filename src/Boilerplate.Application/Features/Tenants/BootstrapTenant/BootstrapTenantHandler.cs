using Boilerplate.Application.Common.Auditing;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Application.Features.Roles;
using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Tenants.BootstrapTenant;

/// <summary>
/// Creates a tenant with its administrator role and first administrator (ADR-0022). Idempotent: anything that
/// already exists is reused, and everything is saved in one transaction.
/// </summary>
public sealed class BootstrapTenantHandler(IAppDbContext db, TenantOverride tenantOverride, AuditLog auditLog)
{
    public async Task<BootstrapTenantResult> HandleAsync(BootstrapTenantRequest request, CancellationToken cancellationToken)
    {
        // The entity normalizes the name; look the tenant up by the normalized value.
        var candidate = Tenant.Create(request.TenantName);
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Name == candidate.Name, cancellationToken);
        var tenantCreated = tenant is null;
        if (tenant is null)
        {
            tenant = candidate;
            db.Tenants.Add(tenant);
        }

        // From here on, tenant-owned reads and writes act for this tenant (no token claim exists in a command).
        tenantOverride.Set(tenant.Id);
        if (tenantCreated)
        {
            auditLog.Record(AuditActions.TenantCreated, tenant.Id, $"Created tenant '{tenant.Name}' with the bootstrap command.");
        }

        var administrators = await db.Roles.SingleOrDefaultAsync(role => role.IsAdministrator, cancellationToken);
        var roleCreated = administrators is null;
        if (administrators is null)
        {
            administrators = Role.CreateAdministrators();
            db.Roles.Add(administrators);
            auditLog.Record(AuditActions.RoleCreated, administrators.Id, RoleAuditing.Created(administrators));
        }

        var alreadyAssigned = !roleCreated && await db.RoleAssignments.AnyAsync(
            assignment => assignment.RoleId == administrators.Id && assignment.UserId == request.AdminUserId,
            cancellationToken);
        if (!alreadyAssigned)
        {
            db.RoleAssignments.Add(RoleAssignment.Create(request.AdminUserId, administrators));
            auditLog.Record(
                AuditActions.RoleAssigned,
                administrators.Id,
                $"Assigned role '{administrators.Name}' to user {request.AdminUserId}.");
        }

        await db.SaveChangesAsync(cancellationToken);

        return new BootstrapTenantResult(tenant.Id, tenantCreated, roleCreated, !alreadyAssigned);
    }
}
