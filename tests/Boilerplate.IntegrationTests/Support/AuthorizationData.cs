using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Tenants;
using Boilerplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// Writes tenants, roles and assignments directly through the production persistence wiring, standing in for
/// role administration (M7) in authorization tests.
/// </summary>
internal sealed class AuthorizationData(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly ServiceProvider _services = PersistenceServices.Create(postgres.ConnectionString);

    public async Task<Guid> CreateTenantAsync(CancellationToken cancellationToken)
    {
        var tenant = Tenant.Create($"authz-{Guid.NewGuid():N}");
        await using var scope = _services.CreateScope(tenantId: null);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return tenant.Id;
    }

    /// <summary>Creates a role granting <paramref name="permissions"/> and assigns it to <paramref name="userId"/>.</summary>
    public async Task<Guid> GrantAsync(Guid tenantId, string userId, string[] permissions, CancellationToken cancellationToken)
    {
        var role = Role.Create($"role-{Guid.NewGuid():N}");
        foreach (var permission in permissions)
        {
            role.Grant(permission);
        }

        await using var scope = _services.CreateScope(tenantId, userId: "admin");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Roles.Add(role);
        db.RoleAssignments.Add(RoleAssignment.Create(userId, role));
        await db.SaveChangesAsync(cancellationToken);
        return role.Id;
    }

    public async Task RevokeAsync(Guid tenantId, Guid roleId, string permission, CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateScope(tenantId, userId: "admin");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.Roles.SingleAsync(r => r.Id == roleId, cancellationToken);
        role.Revoke(permission);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnassignAsync(Guid tenantId, string userId, Guid roleId, CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateScope(tenantId, userId: "admin");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignment = await db.RoleAssignments
            .SingleAsync(a => a.UserId == userId && a.RoleId == roleId, cancellationToken);
        db.RoleAssignments.Remove(assignment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
