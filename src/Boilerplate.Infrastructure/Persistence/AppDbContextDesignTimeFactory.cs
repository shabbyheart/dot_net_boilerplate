using Boilerplate.Application.Common.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Boilerplate.Infrastructure.Persistence;

/// <summary>
/// Lets the dotnet-ef tool build the model without starting the API (ADR-0011).
/// Commands that connect (for example <c>database update</c>) read the
/// <c>Database__ConnectionString</c> environment variable.
/// </summary>
internal sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string ModelOnlyConnectionString = "Host=localhost;Database=boilerplate";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("Database__ConnectionString") is { Length: > 0 } configured
            ? configured
            : ModelOnlyConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAppDatabase(connectionString);

        return new AppDbContext(options.Options, new NoTenantContext());
    }

    private sealed class NoTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
    }
}
