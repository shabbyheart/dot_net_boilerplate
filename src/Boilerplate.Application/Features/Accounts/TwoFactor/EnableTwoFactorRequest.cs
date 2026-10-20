namespace Boilerplate.Application.Features.Accounts.TwoFactor;

/// <param name="Password">The account's current password.</param>
/// <param name="Code">A code from the authenticator set up with the latest key.</param>
public sealed record EnableTwoFactorRequest(string Password, string Code);
