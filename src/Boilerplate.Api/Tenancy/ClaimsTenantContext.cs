using Boilerplate.Application.Common.Tenancy;

namespace Boilerplate.Api.Tenancy;

/// <summary>
/// The current tenant (ADR-0010): an explicit <see cref="TenantOverride"/> when set (commands, background work),
/// otherwise the authenticated user's <c>tenant_id</c> claim. Anonymous requests, or a missing or malformed claim,
/// mean no tenant.
/// </summary>
internal sealed class ClaimsTenantContext(IHttpContextAccessor httpContextAccessor, TenantOverride tenantOverride)
    : ITenantContext
{
    public const string TenantIdClaimType = "tenant_id";

    public Guid? TenantId
    {
        get
        {
            if (tenantOverride.TenantId is { } explicitTenant)
            {
                return explicitTenant;
            }

            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return Guid.TryParse(user.FindFirst(TenantIdClaimType)?.Value, out var tenantId) ? tenantId : null;
        }
    }
}
