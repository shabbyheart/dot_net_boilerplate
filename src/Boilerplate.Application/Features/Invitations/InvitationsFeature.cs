using Boilerplate.Application.Features.Invitations.AcceptInvitation;
using Boilerplate.Application.Features.Invitations.CreateInvitation;
using Boilerplate.Application.Features.Invitations.GetInvitation;
using Boilerplate.Application.Features.Invitations.ListInvitations;
using Boilerplate.Application.Features.Invitations.RevokeInvitation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Application.Features.Invitations;

/// <summary>
/// Registers invitations explicitly (ADR-0004, ADR-0027). Infrastructure provides <see cref="InvitationSettings"/>.
/// </summary>
public static class InvitationsFeature
{
    public static IServiceCollection AddInvitationsFeature(this IServiceCollection services)
    {
        services.AddScoped<InvitationSender>();

        services.AddScoped<CreateInvitationHandler>();
        services.AddScoped<GetInvitationHandler>();
        services.AddScoped<ListInvitationsHandler>();
        services.AddScoped<RevokeInvitationHandler>();
        services.AddScoped<AcceptInvitationHandler>();

        services.AddScoped<IValidator<CreateInvitationRequest>, CreateInvitationValidator>();
        services.AddScoped<IValidator<ListInvitationsRequest>, ListInvitationsValidator>();
        services.AddScoped<IValidator<AcceptInvitationRequest>, AcceptInvitationValidator>();

        return services;
    }
}
