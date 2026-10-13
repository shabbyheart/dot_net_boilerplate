namespace Boilerplate.Application.Features.Accounts.Login;

/// <param name="Email">The account's e-mail address.</param>
/// <param name="Password">The account's password.</param>
/// <param name="TenantId">
/// The tenant to act for; the user must hold a role in it. Without it, the token identifies the user only.
/// </param>
public sealed record LoginRequest(string Email, string Password, Guid? TenantId);
