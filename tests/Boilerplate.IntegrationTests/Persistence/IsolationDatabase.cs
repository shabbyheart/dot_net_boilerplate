using Boilerplate.Domain.Tenants;
using Boilerplate.Infrastructure;
using Boilerplate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Persistence;

/// <summary>
/// A separate database containing the production model plus <see cref="TestNote"/>,
/// wired exactly like production (<see cref="DependencyInjection.ConfigureDbContext"/>).
/// </summary>
public sealed class IsolationDatabase(PostgresFixture postgres) : IAsyncLifetime
{
    public IServiceProvider Services { get; private set; } = null!;

    private ServiceProvider? _provider;

    public async ValueTask InitializeAsync()
    {
        _provider = PersistenceServices.Create(
            postgres.ConnectionStringFor("isolation_tests"),
            configure: services => services.AddDbContext<IsolationTestDbContext>(DependencyInjection.ConfigureDbContext));
        Services = _provider;

        await using var scope = Services.CreateScope(tenantId: null);
        await scope.ServiceProvider.GetRequiredService<IsolationTestDbContext>().Database.EnsureCreatedAsync();
    }

    /// <summary>Creates two new tenants, so each test has data no other test touches.</summary>
    public async Task<(Guid TenantA, Guid TenantB)> CreateTwoTenantsAsync(CancellationToken cancellationToken)
    {
        var tenantA = Tenant.Create($"tenant-a-{Guid.NewGuid():N}");
        var tenantB = Tenant.Create($"tenant-b-{Guid.NewGuid():N}");

        await using var scope = Services.CreateScope(tenantId: null);
        var db = scope.ServiceProvider.GetRequiredService<IsolationTestDbContext>();
        db.Tenants.AddRange(tenantA, tenantB);
        await db.SaveChangesAsync(cancellationToken);

        return (tenantA.Id, tenantB.Id);
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }
    }
}
