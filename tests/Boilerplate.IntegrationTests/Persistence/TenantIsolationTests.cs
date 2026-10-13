using Boilerplate.Application.Common.Exceptions;
using Boilerplate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Persistence;

/// <summary>Tenant isolation rules from ADR-0010, exercised against real PostgreSQL.</summary>
public sealed class TenantIsolationTests(IsolationDatabase database) : IClassFixture<IsolationDatabase>
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Queries_return_only_rows_of_the_current_tenant()
    {
        var (tenantA, tenantB) = await database.CreateTwoTenantsAsync(_cancellationToken);
        await AddNoteAsync(tenantA, "a-note");
        await AddNoteAsync(tenantB, "b-note");

        await using var scope = database.Services.CreateScope(tenantA);
        var notes = await Db(scope).Notes.AsNoTracking().ToListAsync(_cancellationToken);

        notes.ShouldNotBeEmpty();
        notes.ShouldAllBe(note => note.TenantId == tenantA);
        notes.Select(note => note.Title).ShouldNotContain("b-note");
    }

    [Fact]
    public async Task Queries_without_a_current_tenant_return_no_rows()
    {
        var (tenantA, _) = await database.CreateTwoTenantsAsync(_cancellationToken);
        await AddNoteAsync(tenantA, "exists");

        await using var scope = database.Services.CreateScope(tenantId: null);

        (await Db(scope).Notes.CountAsync(_cancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task Insert_stamps_the_current_tenant()
    {
        var (tenantA, _) = await database.CreateTwoTenantsAsync(_cancellationToken);

        var note = await AddNoteAsync(tenantA, "stamped");

        (await LoadIgnoringTenantAsync(note.Id)).TenantId.ShouldBe(tenantA);
    }

    [Fact]
    public async Task Insert_for_another_tenant_is_rejected()
    {
        var (tenantA, tenantB) = await database.CreateTwoTenantsAsync(_cancellationToken);

        await using var scope = database.Services.CreateScope(tenantA);
        var db = Db(scope);
        db.Notes.Add(TestNote.ForTenant("forged", tenantB));

        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(_cancellationToken));
    }

    [Fact]
    public async Task Insert_without_a_current_tenant_is_rejected()
    {
        await using var scope = database.Services.CreateScope(tenantId: null);
        var db = Db(scope);
        db.Notes.Add(new TestNote("orphan"));

        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(_cancellationToken));
    }

    [Fact]
    public async Task Updating_another_tenants_row_is_rejected()
    {
        var (tenantA, tenantB) = await database.CreateTwoTenantsAsync(_cancellationToken);
        var noteOfB = await AddNoteAsync(tenantB, "original");

        await using (var scope = database.Services.CreateScope(tenantA))
        {
            var db = Db(scope);
            db.Notes.Update(noteOfB);
            noteOfB.Rename("hijacked");

            await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(_cancellationToken));
        }

        (await LoadIgnoringTenantAsync(noteOfB.Id)).Title.ShouldBe("original");
    }

    [Fact]
    public async Task Deleting_another_tenants_row_is_rejected()
    {
        var (tenantA, tenantB) = await database.CreateTwoTenantsAsync(_cancellationToken);
        var noteOfB = await AddNoteAsync(tenantB, "keep me");

        await using (var scope = database.Services.CreateScope(tenantA))
        {
            var db = Db(scope);
            db.Notes.Remove(noteOfB);

            await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(_cancellationToken));
        }

        (await LoadIgnoringTenantAsync(noteOfB.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Moving_a_row_to_another_tenant_is_rejected()
    {
        var (tenantA, tenantB) = await database.CreateTwoTenantsAsync(_cancellationToken);
        var created = await AddNoteAsync(tenantA, "mine");

        await using var scope = database.Services.CreateScope(tenantA);
        var db = Db(scope);
        var note = await db.Notes.SingleAsync(n => n.Id == created.Id, _cancellationToken);
        db.Entry(note).Property(n => n.TenantId).CurrentValue = tenantB;

        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(_cancellationToken));
    }

    [Fact]
    public async Task Bulk_delete_affects_only_the_current_tenant()
    {
        var (tenantA, tenantB) = await database.CreateTwoTenantsAsync(_cancellationToken);
        await AddNoteAsync(tenantA, "bulk");
        var noteOfB = await AddNoteAsync(tenantB, "bulk");

        await using (var scope = database.Services.CreateScope(tenantA))
        {
            var deleted = await Db(scope).Notes.Where(n => n.Title == "bulk").ExecuteDeleteAsync(_cancellationToken);
            deleted.ShouldBe(1);
        }

        (await LoadIgnoringTenantAsync(noteOfB.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Bulk_update_affects_only_the_current_tenant()
    {
        var (tenantA, tenantB) = await database.CreateTwoTenantsAsync(_cancellationToken);
        await AddNoteAsync(tenantA, "before");
        var noteOfB = await AddNoteAsync(tenantB, "before");

        await using (var scope = database.Services.CreateScope(tenantA))
        {
            var updated = await Db(scope).Notes
                .Where(n => n.Title == "before")
                .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.Title, "after"), _cancellationToken);
            updated.ShouldBe(1);
        }

        (await LoadIgnoringTenantAsync(noteOfB.Id)).Title.ShouldBe("before");
    }

    [Fact]
    public async Task Unique_constraints_apply_per_tenant()
    {
        var (tenantA, tenantB) = await database.CreateTwoTenantsAsync(_cancellationToken);
        await AddNoteAsync(tenantA, "same title");

        // The same value in another tenant is allowed.
        await AddNoteAsync(tenantB, "same title");

        // A duplicate within the same tenant is not; the database violation surfaces as a 409-mapped ConflictException.
        await Should.ThrowAsync<ConflictException>(() => AddNoteAsync(tenantA, "same title"));
    }

    private static IsolationTestDbContext Db(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IsolationTestDbContext>();

    private async Task<TestNote> AddNoteAsync(Guid tenantId, string title)
    {
        await using var scope = database.Services.CreateScope(tenantId);
        var db = Db(scope);
        var note = new TestNote(title);
        db.Notes.Add(note);
        await db.SaveChangesAsync(_cancellationToken);
        return note;
    }

    // Verification only: reads the stored row regardless of tenant, the way a reviewed admin path would.
    private async Task<TestNote> LoadIgnoringTenantAsync(Guid noteId)
    {
        await using var scope = database.Services.CreateScope(tenantId: null);
        return await Db(scope).Notes
            .IgnoreQueryFilters([IsolationTestDbContext.TenantQueryFilter])
            .AsNoTracking()
            .SingleAsync(n => n.Id == noteId, _cancellationToken);
    }
}
