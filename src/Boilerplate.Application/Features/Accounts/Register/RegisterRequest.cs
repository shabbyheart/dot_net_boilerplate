namespace Boilerplate.Application.Features.Accounts.Register;

/// <param name="Email">The sign-in name; a confirmation link is sent to it.</param>
/// <param name="Password">12 to 128 characters.</param>
public sealed record RegisterRequest(string Email, string Password);
