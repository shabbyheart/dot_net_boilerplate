using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Tenants.ListTenants;

public sealed class ListTenantsHandler(IAppDbContext db)
{
    public Task<PagedResponse<TenantResponse>> HandleAsync(ListTenantsRequest request, CancellationToken cancellationToken) =>
        db.Tenants
            .AsNoTracking()
            .OrderBy(tenant => tenant.Name)
            .ThenBy(tenant => tenant.Id)
            .Select(TenantResponse.FromTenant)
            .ToPagedResponseAsync(request, cancellationToken);
}
