using Boilerplate.Domain.Tenants;
using FluentValidation;

namespace Boilerplate.Application.Features.Tenants.RenameTenant;

public sealed class RenameTenantValidator : AbstractValidator<RenameTenantRequest>
{
    public RenameTenantValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(Tenant.NameMaxLength);
    }
}
