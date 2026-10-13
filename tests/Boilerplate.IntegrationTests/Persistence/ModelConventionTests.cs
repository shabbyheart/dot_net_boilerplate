using Boilerplate.Domain.Common;
using Boilerplate.Domain.Tenants;
using Boilerplate.Infrastructure;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Persistence;

/// <summary>
/// Model-level rules from ADR-0010/0011/0012. Building the model needs no database connection.
/// </summary>
public sealed class ModelConventionTests
{
    private const string UnusedConnectionString = "Host=model-only;Database=model-only";

    [Fact]
    public void Every_string_property_has_an_explicit_length_decision()
    {
        var undecided = StringProperties(ProductionModel())
            .Where(property => property.GetMaxLength() is null && property.GetColumnType() != "text")
            .Select(Describe);

        undecided.ShouldBeEmpty("Give each string a maximum length, or the 'text' column type for free-form text.");
    }

    [Fact]
    public void Entity_ids_are_never_generated_by_the_database()
    {
        var generated = ProductionModel().GetEntityTypes()
            .Where(entityType => typeof(Entity).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.FindProperty(nameof(Entity.Id))!)
            .Where(id => id.ValueGenerated != ValueGenerated.Never)
            .Select(Describe);

        generated.ShouldBeEmpty();
    }

    [Fact]
    public void Tenant_owned_entities_get_filter_foreign_key_and_index()
    {
        // TestNote proves the convention; the loop also checks every production tenant-owned entity.
        using var services = PersistenceServices.Create(
            UnusedConnectionString,
            configure: s => s.AddDbContext<IsolationTestDbContext>(DependencyInjection.ConfigureDbContext));
        using var scope = services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<IsolationTestDbContext>().Model;

        var tenantOwned = model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            .ToList();

        tenantOwned.ShouldContain(entityType => entityType.ClrType == typeof(TestNote));

        foreach (var entityType in tenantOwned)
        {
            entityType.FindDeclaredQueryFilter(AppDbContext.TenantQueryFilter)
                .ShouldNotBeNull($"{entityType.ClrType.Name} has no tenant query filter.");

            entityType.GetForeignKeys()
                .ShouldContain(
                    foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Tenant)
                        && foreignKey.Properties.Single().Name == nameof(ITenantOwned.TenantId),
                    $"{entityType.ClrType.Name} has no foreign key to Tenant.");

            entityType.GetIndexes()
                .ShouldContain(
                    index => index.Properties[0].Name == nameof(ITenantOwned.TenantId),
                    $"{entityType.ClrType.Name} has no index starting with TenantId.");
        }
    }

    private static IModel ProductionModel()
    {
        using var services = PersistenceServices.Create(UnusedConnectionString);
        using var scope = services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Model;
    }

    private static IEnumerable<IProperty> StringProperties(IModel model) =>
        model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties())
            .Where(property => property.ClrType == typeof(string));

    private static string Describe(IProperty property) =>
        $"{property.DeclaringType.ClrType.Name}.{property.Name}";
}
