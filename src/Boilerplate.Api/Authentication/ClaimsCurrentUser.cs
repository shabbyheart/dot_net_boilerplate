using Boilerplate.Application.Common.Security;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Boilerplate.Api.Authentication;

/// <summary>The current user for HTTP requests: the authenticated user's <c>sub</c> claim (ADR-0016).</summary>
internal sealed class ClaimsCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true
                ? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                : null;
        }
    }
}
