using Boilerplate.Application.Common.Messaging;
using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Projects;
using Boilerplate.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Common.Persistence;

/// <summary>
/// The database as seen by use cases (ADR-0005): one set per aggregate root and a single commit point.
/// Tenant isolation is applied by the implementation, so handlers never filter by tenant themselves (ADR-0010).
/// </summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<Role> Roles { get; }

    DbSet<RoleAssignment> RoleAssignments { get; }

    DbSet<Project> Projects { get; }

    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
