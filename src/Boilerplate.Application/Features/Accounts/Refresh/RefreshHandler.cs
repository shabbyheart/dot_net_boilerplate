using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Accounts.Refresh;

public sealed class RefreshHandler(
    IAppDbContext db,
    IUserAccounts accounts,
    AccountSessions sessions,
    TimeProvider timeProvider)
{
    public const string FailedMessage = "The refresh token is not valid. Sign in again.";

    /// <summary>Exchanges an active refresh token for a new access and refresh token pair.</summary>
    /// <exception cref="AuthenticationFailedException">The token is unknown, used, revoked or expired, or the user may no longer sign in.</exception>
    public async Task<TokenResponse> HandleAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var hash = SecureToken.Hash(request.RefreshToken);
        var current = await db.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken)
            ?? throw new AuthenticationFailedException(FailedMessage);

        // A used token presented again was copied: whoever holds the newer tokens of this family may be an attacker.
        if (current.RevokedAt is not null)
        {
            await RevokeFamilyAndFailAsync(current.FamilyId, cancellationToken);
        }

        if (!current.IsActive(timeProvider.GetUtcNow()))
        {
            throw new AuthenticationFailedException(FailedMessage);
        }

        // Locking, a password reset or losing the tenant membership ends the session at its next refresh.
        var mayContinue = await accounts.CanSignInAsync(current.UserId, cancellationToken)
            && (current.TenantId is not { } tenantId
                || await db.RoleAssignments.IsTenantMemberAsync(current.UserId, tenantId, cancellationToken));
        if (!mayContinue)
        {
            await RevokeFamilyAndFailAsync(current.FamilyId, cancellationToken);
        }

        var response = sessions.Rotate(current);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            // A concurrent refresh with the same token won (the row's version changed); this one must not succeed too.
            throw new AuthenticationFailedException(FailedMessage);
        }

        return response;
    }

    private async Task RevokeFamilyAndFailAsync(Guid familyId, CancellationToken cancellationToken)
    {
        await sessions.RevokeFamilyAsync(familyId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        throw new AuthenticationFailedException(FailedMessage);
    }
}
