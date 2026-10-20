using Boilerplate.Application.Common.Email;
using Boilerplate.Application.Common.Messaging;
using Boilerplate.Application.Common.Security;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Application.Features.Accounts;
using Boilerplate.Application.Features.Auditing;
using Boilerplate.Application.Features.Invitations;
using Boilerplate.Application.Features.Members;
using Boilerplate.Application.Features.PlatformAdministrators;
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
        services.AddScoped<PlatformAdministratorChecker>();
        services.AddScoped<TenantOverride>();

        services.AddOutbox();
        services.AddOutboxMessage<EmailMessage, SendEmailHandler>(EmailMessage.OutboxType);

        services.AddAuditingFeature();
        services.AddProjectsFeature();
        services.AddRolesFeature();
        services.AddTenantsFeature();
        services.AddInvitationsFeature();
        services.AddMembersFeature();
        services.AddScoped<PlatformAdministratorGrants>();
        services.AddAccountsFeature();

        return services;
    }
}
