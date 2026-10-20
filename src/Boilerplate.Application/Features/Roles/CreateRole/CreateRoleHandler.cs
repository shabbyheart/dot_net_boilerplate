using Boilerplate.Application.Common.Auditing;
using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Domain.Authorization;

namespace Boilerplate.Application.Features.Roles.CreateRole;

public sealed class CreateRoleHandler(IAppDbContext db, RoleEscalationGuard guard, AuditLog auditLog)
{
    /// <exception cref="ForbiddenException">The role would grant a permission the caller does not hold.</exception>
    /// <exception cref="ConflictException">Another role already uses the name.</exception>
    public async Task<RoleResponse> HandleAsync(SaveRoleRequest request, CancellationToken cancellationToken)
    {
        await guard.EnsureCanChangeAsync(request.PermissionsOrEmpty, cancellationToken);

        var role = Role.Create(request.Name);
        role.ReplacePermissions(request.PermissionsOrEmpty);

        if (await db.Roles.NameIsTakenAsync(role.Name, role.Id, cancellationToken))
        {
            throw new ConflictException($"A role named '{role.Name}' already exists.");
        }

        db.Roles.Add(role);
        auditLog.Record(AuditActions.RoleCreated, role.Id, RoleAuditing.Created(role));
        await db.SaveChangesAsync(cancellationToken);

        return RoleResponse.From(role);
    }
}
