using Boilerplate.Infrastructure.Persistence;
using Boilerplate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Persistence;

/// <summary>The migrations create the current model from an empty database (ADR-0011).</summary>
public sealed class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrations_are_applied_and_match_the_model()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var services = PersistenceServices.Create(postgres.ConnectionString);
        await using var scope = services.CreateScope(tenantId: null);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ShouldBeEmpty();

        // Fails when the model changed without a new migration.
        db.Database.HasPendingModelChanges().ShouldBeFalse();
    }
}
