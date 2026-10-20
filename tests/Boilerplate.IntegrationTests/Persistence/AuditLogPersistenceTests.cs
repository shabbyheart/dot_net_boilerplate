using Boilerplate.Domain.Auditing;
using Boilerplate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Persistence;

/// <summary>The audit log is tenant-owned (ADR-0010) and append-only (ADR-0028), against real PostgreSQL.</summary>
public sealed class AuditLogPersistenceTests(IsolationDatabase database) : IClassFixture<IsolationDatabase>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Entries_are_read_only_in_their_tenant()
    {
        var (tenantA, tenantB) = await database.CreateTwoTenantsAsync(_cancellationToken);
        var entryOfA = await AddEntryAsync(tenantA);
        await AddEntryAsync(tenantB);

        await using var scope = database.Services.CreateScope(tenantA);

        (await Db(scope).AuditLogEntries.Select(entry => entry.Id).ToListAsync(_cancellationToken)).ShouldBe([entryOfA.Id]);
    }

    [Fact]
    public async Task Entries_are_stamped_with_the_current_tenant()
    {
        var (tenantA, _) = await database.CreateTwoTenantsAsync(_cancellationToken);

        var entry = await AddEntryAsync(tenantA);

        entry.TenantId.ShouldBe(tenantA);
    }

    [Fact]
    public async Task Changing_an_entry_is_rejected()
    {
        var (tenantA, _) = await database.CreateTwoTenantsAsync(_cancellationToken);
        var created = await AddEntryAsync(tenantA);

        await using var scope = database.Services.CreateScope(tenantA);
        var db = Db(scope);
        var entry = await db.AuditLogEntries.SingleAsync(e => e.Id == created.Id, _cancellationToken);
        db.Entry(entry).Property(e => e.Description).CurrentValue = "rewritten";

        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(_cancellationToken));
    }

    [Fact]
    public async Task Deleting_an_entry_is_rejected()
    {
        var (tenantA, _) = await database.CreateTwoTenantsAsync(_cancellationToken);
        var created = await AddEntryAsync(tenantA);

        await using (var scope = database.Services.CreateScope(tenantA))
        {
            var db = Db(scope);
            db.AuditLogEntries.Remove(await db.AuditLogEntries.SingleAsync(e => e.Id == created.Id, _cancellationToken));

            await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(_cancellationToken));
        }

        await using var verify = database.Services.CreateScope(tenantA);
        (await Db(verify).AuditLogEntries.AnyAsync(e => e.Id == created.Id, _cancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task Bulk_delete_affects_only_the_current_tenant()
    {
        var (tenantA, tenantB) = await database.CreateTwoTenantsAsync(_cancellationToken);
        await AddEntryAsync(tenantA);
        var entryOfB = await AddEntryAsync(tenantB);

        await using (var scope = database.Services.CreateScope(tenantA))
        {
            (await Db(scope).AuditLogEntries.ExecuteDeleteAsync(_cancellationToken)).ShouldBe(1);
        }

        await using var verify = database.Services.CreateScope(tenantB);
        (await Db(verify).AuditLogEntries.AnyAsync(e => e.Id == entryOfB.Id, _cancellationToken)).ShouldBeTrue();
    }

    private static IsolationTestDbContext Db(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IsolationTestDbContext>();

    private async Task<AuditLogEntry> AddEntryAsync(Guid tenantId)
    {
        await using var scope = database.Services.CreateScope(tenantId);
        var db = Db(scope);
        var entry = AuditLogEntry.Record(Now, "user-1", "role.created", Guid.NewGuid(), "Created role 'Editors'.");
        db.AuditLogEntries.Add(entry);
        await db.SaveChangesAsync(_cancellationToken);
        return entry;
    }
}
