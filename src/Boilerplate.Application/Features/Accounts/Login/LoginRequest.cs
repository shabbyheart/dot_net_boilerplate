namespace Boilerplate.Application.Features.Accounts.Login;

/// <param name="Email">The account's e-mail address.</param>
/// <param name="Password">The account's password.</param>
/// <param name="TenantId">
/// The tenant to act for; the user must hold a role in it. Without it, the token identifies the user only.
/// </param>
/// <param name="TwoFactorCode">For accounts with two-factor sign-in: the code from the authenticator app (ADR-0031).</param>
/// <param name="RecoveryCode">For accounts with two-factor sign-in, instead of a code: an unused recovery code.</param>
public sealed record LoginRequest(
    string Email,
    string Password,
    Guid? TenantId,
    string? TwoFactorCode = null,
    string? RecoveryCode = null);
