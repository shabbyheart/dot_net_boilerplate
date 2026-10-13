using Boilerplate.Application.Features.Roles.AssignRole;
using Boilerplate.Application.Features.Roles.CreateRole;
using Boilerplate.Application.Features.Roles.DeleteRole;
using Boilerplate.Application.Features.Roles.GetRole;
using Boilerplate.Application.Features.Roles.ListRoleAssignments;
using Boilerplate.Application.Features.Roles.ListRoles;
using Boilerplate.Application.Features.Roles.UnassignRole;
using Boilerplate.Application.Features.Roles.UpdateRole;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Application.Features.Roles;

/// <summary>Registers role administration explicitly (ADR-0004, ADR-0022).</summary>
public static class RolesFeature
{
    public static IServiceCollection AddRolesFeature(this IServiceCollection services)
    {
        services.AddScoped<RoleEscalationGuard>();

        services.AddScoped<ListRolesHandler>();
        services.AddScoped<GetRoleHandler>();
        services.AddScoped<CreateRoleHandler>();
        services.AddScoped<UpdateRoleHandler>();
        services.AddScoped<DeleteRoleHandler>();
        services.AddScoped<ListRoleAssignmentsHandler>();
        services.AddScoped<AssignRoleHandler>();
        services.AddScoped<UnassignRoleHandler>();

        services.AddScoped<IValidator<SaveRoleRequest>, SaveRoleValidator>();
        services.AddScoped<IValidator<ListRolesRequest>, ListRolesValidator>();
        services.AddScoped<IValidator<ListRoleAssignmentsRequest>, ListRoleAssignmentsValidator>();

        return services;
    }
}
