using Boilerplate.Application.Features.Projects.CreateProject;
using Boilerplate.Application.Features.Projects.DeleteProject;
using Boilerplate.Application.Features.Projects.GetProject;
using Boilerplate.Application.Features.Projects.ListProjects;
using Boilerplate.Application.Features.Projects.UpdateProject;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Application.Features.Projects;

/// <summary>Registers the projects feature explicitly (ADR-0004, ADR-0014).</summary>
public static class ProjectsFeature
{
    public static IServiceCollection AddProjectsFeature(this IServiceCollection services)
    {
        services.AddScoped<CreateProjectHandler>();
        services.AddScoped<GetProjectHandler>();
        services.AddScoped<ListProjectsHandler>();
        services.AddScoped<UpdateProjectHandler>();
        services.AddScoped<DeleteProjectHandler>();

        services.AddScoped<IValidator<CreateProjectRequest>, CreateProjectValidator>();
        services.AddScoped<IValidator<ListProjectsRequest>, ListProjectsValidator>();
        services.AddScoped<IValidator<UpdateProjectRequest>, UpdateProjectValidator>();

        return services;
    }
}
