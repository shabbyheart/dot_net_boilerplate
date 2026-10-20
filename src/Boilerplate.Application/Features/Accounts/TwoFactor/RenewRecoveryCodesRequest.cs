namespace Boilerplate.Application.Features.Accounts.TwoFactor;

/// <param name="Password">The account's current password.</param>
/// <param name="Code">A code from the authenticator app.</param>
public sealed record RenewRecoveryCodesRequest(string Password, string Code);
