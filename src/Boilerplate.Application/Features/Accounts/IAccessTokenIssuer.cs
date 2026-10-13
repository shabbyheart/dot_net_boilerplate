namespace Boilerplate.Application.Features.Accounts;

/// <summary>Creates signed access tokens that the API itself accepts (ADR-0025). Implemented in Infrastructure.</summary>
public interface IAccessTokenIssuer
{
    /// <param name="userId">Becomes the <c>sub</c> claim.</param>
    /// <param name="tenantId">Becomes the <c>tenant_id</c> claim when set.</param>
    AccessToken Issue(string userId, Guid? tenantId);
}
