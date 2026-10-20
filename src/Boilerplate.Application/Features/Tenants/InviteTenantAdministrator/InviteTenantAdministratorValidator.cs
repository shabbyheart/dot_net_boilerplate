using Boilerplate.Application.Features.Invitations;
using FluentValidation;

namespace Boilerplate.Application.Features.Tenants.InviteTenantAdministrator;

public sealed class InviteTenantAdministratorValidator : AbstractValidator<InviteTenantAdministratorRequest>
{
    public InviteTenantAdministratorValidator()
    {
        RuleFor(request => request.Email).InvitedEmail();
    }
}
