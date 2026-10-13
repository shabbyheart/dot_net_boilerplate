using Boilerplate.Application.Common.Tenancy;

namespace Boilerplate.Api.Tenancy;

/// <summary>
/// The current tenant for HTTP requests: the authenticated user's <c>tenant_id</c> claim (ADR-0010).
/// Anonymous requests, or a missing or malformed claim, mean no tenant.
/// </summary>
internal sealed class ClaimsTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public const string TenantIdClaimType = "tenant_id";

    public Guid? TenantId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return Guid.TryParse(user.FindFirst(TenantIdClaimType)?.Value, out var tenantId) ? tenantId : null;
        }
    }
}
