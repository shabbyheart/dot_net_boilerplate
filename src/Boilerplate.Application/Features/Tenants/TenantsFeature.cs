using Boilerplate.Application.Features.Tenants.BootstrapTenant;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Application.Features.Tenants;

/// <summary>Registers tenant use cases explicitly (ADR-0004).</summary>
public static class TenantsFeature
{
    public static IServiceCollection AddTenantsFeature(this IServiceCollection services)
    {
        services.AddScoped<BootstrapTenantHandler>();
        services.AddScoped<IValidator<BootstrapTenantRequest>, BootstrapTenantValidator>();

        return services;
    }
}
