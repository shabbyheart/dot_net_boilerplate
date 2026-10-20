using Boilerplate.Application.Features.Members.GetMember;
using Boilerplate.Application.Features.Members.LeaveTenant;
using Boilerplate.Application.Features.Members.ListMembers;
using Boilerplate.Application.Features.Members.RemoveMember;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Application.Features.Members;

/// <summary>Registers tenant membership explicitly (ADR-0004, ADR-0029).</summary>
public static class MembersFeature
{
    public static IServiceCollection AddMembersFeature(this IServiceCollection services)
    {
        services.AddScoped<MemberRemoval>();

        services.AddScoped<ListMembersHandler>();
        services.AddScoped<GetMemberHandler>();
        services.AddScoped<RemoveMemberHandler>();
        services.AddScoped<LeaveTenantHandler>();

        services.AddScoped<IValidator<ListMembersRequest>, ListMembersValidator>();

        return services;
    }
}
