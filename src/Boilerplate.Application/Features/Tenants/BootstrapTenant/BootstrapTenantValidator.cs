using Boilerplate.Domain.Common;
using Boilerplate.Domain.Tenants;
using FluentValidation;

namespace Boilerplate.Application.Features.Tenants.BootstrapTenant;

public sealed class BootstrapTenantValidator : AbstractValidator<BootstrapTenantRequest>
{
    public BootstrapTenantValidator()
    {
        RuleFor(request => request.TenantName).NotEmpty().MaximumLength(Tenant.NameMaxLength);
        RuleFor(request => request.AdminUserId).NotEmpty().MaximumLength(IAuditable.UserIdMaxLength);
    }
}
