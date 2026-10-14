using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;

namespace Boilerplate.Application.Features.Tenants.RenameTenant;

public sealed class RenameTenantHandler(IAppDbContext db)
{
    /// <exception cref="NotFoundException">No tenant with this id exists.</exception>
    /// <exception cref="ConflictException">Another tenant already has the name.</exception>
    public async Task<TenantResponse> HandleAsync(Guid tenantId, RenameTenantRequest request, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.GetTenantAsync(tenantId, cancellationToken);
        tenant.Rename(request.Name);

        if (await db.Tenants.NameIsTakenAsync(tenant.Name, tenant.Id, cancellationToken))
        {
            throw new ConflictException($"A tenant named '{tenant.Name}' already exists.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return TenantResponse.From(tenant);
    }
}
