namespace Boilerplate.Application.Common.Tenancy;

/// <summary>The access token claim that names the caller's tenant (ADR-0010), read by the API and written by local sign-in.</summary>
public static class TenantClaim
{
    public const string Type = "tenant_id";
}
