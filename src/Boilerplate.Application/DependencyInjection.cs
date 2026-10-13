using Boilerplate.Application.Common.Security;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Application.Features.Projects;
using Boilerplate.Application.Features.Roles;
using Boilerplate.Application.Features.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Application;

/// <summary>Registers Application services (ADR-0004).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<PermissionChecker>();
        services.AddScoped<TenantOverride>();

        services.AddProjectsFeature();
        services.AddRolesFeature();
        services.AddTenantsFeature();

        return services;
    }
}
