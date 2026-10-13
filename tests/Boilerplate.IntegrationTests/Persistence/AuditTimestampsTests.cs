using Boilerplate.Domain.Tenants;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Boilerplate.IntegrationTests.Persistence;

/// <summary>Timestamps come from the injected TimeProvider (ADR-0012).</summary>
public sealed class AuditTimestampsTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Created_and_updated_times_are_set_on_save()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var createdAt = new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(createdAt);
        await using var services = PersistenceServices.Create(postgres.ConnectionString, clock);

        var tenant = Tenant.Create($"timestamps-{Guid.NewGuid():N}");
        await using (var scope = services.CreateScope(tenantId: null))
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(cancellationToken);
        }

        clock.Advance(TimeSpan.FromMinutes(5));
        await using (var scope = services.CreateScope(tenantId: null))
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Tenants.SingleAsync(t => t.Id == tenant.Id, cancellationToken);
            stored.Rename("renamed");
            await db.SaveChangesAsync(cancellationToken);
        }

        await using (var scope = services.CreateScope(tenantId: null))
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenant.Id, cancellationToken);

            stored.CreatedAt.ShouldBe(createdAt);
            stored.UpdatedAt.ShouldBe(createdAt.AddMinutes(5));
        }
    }
}
