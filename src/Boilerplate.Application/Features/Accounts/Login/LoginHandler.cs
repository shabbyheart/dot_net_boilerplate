using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Persistence;

namespace Boilerplate.Application.Features.Accounts.Login;

public sealed class LoginHandler(IAppDbContext db, IUserAccounts accounts, AccountSessions sessions)
{
    /// <summary>The same message for unknown addresses, wrong passwords, wrong codes and locked accounts.</summary>
    public const string FailedMessage = "The e-mail address or password is incorrect, or the account is temporarily locked.";

    public const string TwoFactorRequiredMessage =
        "This account uses two-factor sign-in. Sign in again with a code from your authenticator app or a recovery code.";

    /// <exception cref="AuthenticationFailedException">Unknown address, wrong password or code, or locked account.</exception>
    /// <exception cref="TwoFactorRequiredException">Correct password, but the account needs a second factor (ADR-0031).</exception>
    /// <exception cref="ForbiddenException">Correct password, but unconfirmed address or not a member of the tenant.</exception>
    public async Task<TokenResponse> HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var check = await accounts.CheckPasswordAsync(
            request.Email,
            request.Password,
            SecondFactor.From(request.TwoFactorCode, request.RecoveryCode),
            cancellationToken);

        // Only callers who know the password learn more than "failed".
        var userId = check.Status switch
        {
            PasswordCheckStatus.Succeeded => check.UserId!,
            PasswordCheckStatus.EmailNotConfirmed => throw new ForbiddenException("Confirm your e-mail address before signing in."),
            PasswordCheckStatus.TwoFactorRequired => throw new TwoFactorRequiredException(TwoFactorRequiredMessage),
            _ => throw new AuthenticationFailedException(FailedMessage),
        };

        if (request.TenantId is { } tenantId
            && !await db.RoleAssignments.IsTenantMemberAsync(userId, tenantId, cancellationToken))
        {
            throw new ForbiddenException("You are not a member of this tenant.");
        }

        var response = sessions.Start(userId, request.TenantId);
        await db.SaveChangesAsync(cancellationToken);
        return response;
    }
}
