using Boilerplate.Domain.Tenants;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Boilerplate.IntegrationTests.Persistence;

/// <summary>Who and when: set on save from ICurrentUser and TimeProvider (ADR-0012, ADR-0016).</summary>
public sealed class AuditTests(PostgresFixture postgres)
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Creation_and_update_are_recorded_with_time_and_user()
    {
        var createdAt = new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(createdAt);
        await using var services = PersistenceServices.Create(postgres.ConnectionString, clock);
        var tenant = Tenant.Create($"audit-{Guid.NewGuid():N}");

        await using (var scope = services.CreateScope(tenantId: null, userId: "creator"))
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(_cancellationToken);
        }

        clock.Advance(TimeSpan.FromMinutes(5));
        await using (var scope = services.CreateScope(tenantId: null, userId: "editor"))
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Tenants.SingleAsync(t => t.Id == tenant.Id, _cancellationToken);
            stored.Rename("renamed");
            await db.SaveChangesAsync(_cancellationToken);
        }

        var result = await LoadAsync(services, tenant.Id);
        result.CreatedAt.ShouldBe(createdAt);
        result.CreatedBy.ShouldBe("creator");
        result.UpdatedAt.ShouldBe(createdAt.AddMinutes(5));
        result.UpdatedBy.ShouldBe("editor");
    }

    [Fact]
    public async Task Operations_without_a_user_record_no_user()
    {
        await using var services = PersistenceServices.Create(postgres.ConnectionString);
        var tenant = Tenant.Create($"audit-system-{Guid.NewGuid():N}");

        await using (var scope = services.CreateScope(tenantId: null, userId: null))
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(_cancellationToken);
        }

        var result = await LoadAsync(services, tenant.Id);
        result.CreatedBy.ShouldBeNull();
        result.UpdatedBy.ShouldBeNull();
    }

    private async Task<Tenant> LoadAsync(IServiceProvider services, Guid tenantId)
    {
        await using var scope = services.CreateScope(tenantId: null);
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Tenants
            .AsNoTracking()
            .SingleAsync(t => t.Id == tenantId, _cancellationToken);
    }
}
