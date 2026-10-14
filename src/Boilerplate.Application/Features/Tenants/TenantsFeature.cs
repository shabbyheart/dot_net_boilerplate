using Boilerplate.Application.Features.Tenants.BootstrapTenant;
using Boilerplate.Application.Features.Tenants.CreateTenant;
using Boilerplate.Application.Features.Tenants.GetTenant;
using Boilerplate.Application.Features.Tenants.InviteTenantAdministrator;
using Boilerplate.Application.Features.Tenants.ListTenants;
using Boilerplate.Application.Features.Tenants.RenameTenant;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Application.Features.Tenants;

/// <summary>Registers tenant use cases explicitly (ADR-0004): bootstrap (ADR-0022) and platform administration (ADR-0027).</summary>
public static class TenantsFeature
{
    public static IServiceCollection AddTenantsFeature(this IServiceCollection services)
    {
        services.AddScoped<BootstrapTenantHandler>();
        services.AddScoped<ListTenantsHandler>();
        services.AddScoped<GetTenantHandler>();
        services.AddScoped<CreateTenantHandler>();
        services.AddScoped<RenameTenantHandler>();
        services.AddScoped<InviteTenantAdministratorHandler>();

        services.AddScoped<IValidator<BootstrapTenantRequest>, BootstrapTenantValidator>();
        services.AddScoped<IValidator<ListTenantsRequest>, ListTenantsValidator>();
        services.AddScoped<IValidator<CreateTenantRequest>, CreateTenantValidator>();
        services.AddScoped<IValidator<RenameTenantRequest>, RenameTenantValidator>();
        services.AddScoped<IValidator<InviteTenantAdministratorRequest>, InviteTenantAdministratorValidator>();

        return services;
    }
}
