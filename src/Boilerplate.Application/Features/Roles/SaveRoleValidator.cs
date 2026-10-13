using Boilerplate.Application.Common.Security;
using Boilerplate.Domain.Authorization;
using FluentValidation;

namespace Boilerplate.Application.Features.Roles;

public sealed class SaveRoleValidator : AbstractValidator<SaveRoleRequest>
{
    public SaveRoleValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(Role.NameMaxLength);

        RuleForEach(request => request.Permissions)
            .Must(permission => permission is not null && PermissionCatalog.IsKnown(permission))
            .WithMessage("'{PropertyValue}' is not a known permission.");
    }
}
