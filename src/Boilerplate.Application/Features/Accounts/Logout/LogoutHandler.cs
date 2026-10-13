using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Accounts.Logout;

public sealed class LogoutHandler(IAppDbContext db, AccountSessions sessions)
{
    /// <summary>
    /// Ends the session the refresh token belongs to. Unknown tokens are ignored, so the result is always the same.
    /// Access tokens already issued stay valid until they expire (ADR-0025).
    /// </summary>
    public async Task HandleAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var hash = SecureToken.Hash(request.RefreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (token is null)
        {
            return;
        }

        await sessions.RevokeFamilyAsync(token.FamilyId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
