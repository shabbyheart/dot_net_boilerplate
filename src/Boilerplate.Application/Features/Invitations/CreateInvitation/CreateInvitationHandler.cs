using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Tenancy;
using Boilerplate.Application.Features.Roles;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Invitations.CreateInvitation;

public sealed class CreateInvitationHandler(
    IAppDbContext db,
    ITenantContext tenantContext,
    RoleEscalationGuard guard,
    InvitationSender sender)
{
    /// <summary>Invites an e-mail address to a role of the current tenant.</summary>
    /// <exception cref="NotFoundException">No role with this id exists in the current tenant.</exception>
    /// <exception cref="ForbiddenException">The caller could not assign the role themselves (ADR-0022).</exception>
    /// <exception cref="ConflictException">Invitations are not configured.</exception>
    public async Task<InvitationResponse> HandleAsync(CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        var role = await db.Roles.GetRoleAsync(request.RoleId, cancellationToken);
        await guard.EnsureCanHandOutAsync(role, cancellationToken);

        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("Inviting requires a current tenant.");
        var tenantName = await db.Tenants
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => tenant.Name)
            .SingleAsync(cancellationToken);

        var invitation = await sender.IssueAsync(role, request.Email, tenantName, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return InvitationResponse.From(invitation);
    }
}
