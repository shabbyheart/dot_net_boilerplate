namespace Boilerplate.Application.Features.Accounts.TwoFactor;

/// <param name="Password">The account's current password.</param>
/// <param name="Code">A code from the authenticator app.</param>
/// <param name="RecoveryCode">Instead of a code: an unused recovery code, for a lost authenticator.</param>
public sealed record DisableTwoFactorRequest(string Password, string? Code, string? RecoveryCode);
