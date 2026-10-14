using Boilerplate.Application.Features.Invitations;
using Boilerplate.Domain.Tenants;
using FluentValidation;

namespace Boilerplate.Application.Features.Tenants.CreateTenant;

public sealed class CreateTenantValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(Tenant.NameMaxLength);
        RuleFor(request => request.AdministratorEmail).InvitedEmail();
    }
}
