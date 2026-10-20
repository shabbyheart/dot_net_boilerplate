using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Tenants.GetTenant;

public sealed class GetTenantHandler(IAppDbContext db)
{
    /// <exception cref="NotFoundException">No tenant with this id exists.</exception>
    public async Task<TenantResponse> HandleAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await db.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(TenantResponse.FromTenant)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Tenant", tenantId);
}
