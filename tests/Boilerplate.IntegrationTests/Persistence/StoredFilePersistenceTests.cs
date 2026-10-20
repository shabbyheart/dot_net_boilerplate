using Boilerplate.Domain.Files;
using Boilerplate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Persistence;

/// <summary>File descriptions are tenant-owned (ADR-0010, ADR-0030), against real PostgreSQL.</summary>
public sealed class StoredFilePersistenceTests(IsolationDatabase database) : IClassFixture<IsolationDatabase>
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Files_are_read_only_in_their_tenant_and_stamped_with_it()
    {
        var (tenantA, tenantB) = await database.CreateTwoTenantsAsync(_cancellationToken);
        var fileOfA = await AddFileAsync(tenantA);
        await AddFileAsync(tenantB);

        await using var scope = database.Services.CreateScope(tenantA);
        var files = await Db(scope).Files.AsNoTracking().ToListAsync(_cancellationToken);

        files.Select(file => file.Id).ShouldBe([fileOfA.Id]);
        files[0].TenantId.ShouldBe(tenantA);
    }

    [Fact]
    public async Task Deleting_another_tenants_file_is_rejected()
    {
        var (tenantA, tenantB) = await database.CreateTwoTenantsAsync(_cancellationToken);
        var fileOfB = await AddFileAsync(tenantB);

        await using var scope = database.Services.CreateScope(tenantA);
        var db = Db(scope);
        db.Files.Remove(fileOfB);

        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(_cancellationToken));
    }

    [Fact]
    public async Task Bulk_delete_affects_only_the_current_tenant()
    {
        var (tenantA, tenantB) = await database.CreateTwoTenantsAsync(_cancellationToken);
        await AddFileAsync(tenantA);
        var fileOfB = await AddFileAsync(tenantB);

        await using (var scope = database.Services.CreateScope(tenantA))
        {
            (await Db(scope).Files.ExecuteDeleteAsync(_cancellationToken)).ShouldBe(1);
        }

        await using var verify = database.Services.CreateScope(tenantB);
        (await Db(verify).Files.AnyAsync(file => file.Id == fileOfB.Id, _cancellationToken)).ShouldBeTrue();
    }

    private static IsolationTestDbContext Db(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IsolationTestDbContext>();

    private async Task<StoredFile> AddFileAsync(Guid tenantId)
    {
        await using var scope = database.Services.CreateScope(tenantId, userId: "user-1");
        var db = Db(scope);
        var file = StoredFile.Create("report.pdf", "application/pdf", 1024);
        db.Files.Add(file);
        await db.SaveChangesAsync(_cancellationToken);
        return file;
    }
}
