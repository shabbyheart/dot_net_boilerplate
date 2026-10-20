namespace Boilerplate.Application.Features.Accounts;

/// <summary>A local account's two-factor state (ADR-0031).</summary>
/// <param name="Email">Where notifications go; also the account name in authenticator apps.</param>
/// <param name="Enabled">Whether sign-in needs a second factor.</param>
/// <param name="RecoveryCodesLeft">Unused recovery codes.</param>
public sealed record TwoFactorAccount(string Email, bool Enabled, int RecoveryCodesLeft);
