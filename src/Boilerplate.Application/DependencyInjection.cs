using Boilerplate.Application.Common.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Application;

/// <summary>Registers Application services (ADR-0004).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<PermissionChecker>();

        return services;
    }
}
