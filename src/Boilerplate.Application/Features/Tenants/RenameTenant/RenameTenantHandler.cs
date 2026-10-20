using Boilerplate.Application.Common.Auditing;
using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Tenancy;

namespace Boilerplate.Application.Features.Tenants.RenameTenant;

public sealed class RenameTenantHandler(IAppDbContext db, TenantOverride tenantOverride, AuditLog auditLog)
{
    /// <exception cref="NotFoundException">No tenant with this id exists.</exception>
    /// <exception cref="ConflictException">Another tenant already has the name.</exception>
    public async Task<TenantResponse> HandleAsync(Guid tenantId, RenameTenantRequest request, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.GetTenantAsync(tenantId, cancellationToken);
        var previousName = tenant.Name;
        tenant.Rename(request.Name);

        if (await db.Tenants.NameIsTakenAsync(tenant.Name, tenant.Id, cancellationToken))
        {
            throw new ConflictException($"A tenant named '{tenant.Name}' already exists.");
        }

        if (!string.Equals(tenant.Name, previousName, StringComparison.Ordinal))
        {
            // The entry belongs to the renamed tenant's audit log, not to the caller's.
            tenantOverride.Set(tenant.Id);
            auditLog.Record(AuditActions.TenantRenamed, tenant.Id, $"Renamed tenant from '{previousName}' to '{tenant.Name}'.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return TenantResponse.From(tenant);
    }
}
