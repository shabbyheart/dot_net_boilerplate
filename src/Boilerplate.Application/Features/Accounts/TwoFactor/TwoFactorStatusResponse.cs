namespace Boilerplate.Application.Features.Accounts.TwoFactor;

/// <param name="Enabled">Whether sign-in needs a second factor.</param>
/// <param name="RecoveryCodesLeft">Unused recovery codes; renew them before they run out.</param>
public sealed record TwoFactorStatusResponse(bool Enabled, int RecoveryCodesLeft);
