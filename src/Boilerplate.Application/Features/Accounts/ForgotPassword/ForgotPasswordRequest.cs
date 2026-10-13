namespace Boilerplate.Application.Features.Accounts.ForgotPassword;

/// <param name="Email">The account's e-mail address; a reset link is sent to it if it has a confirmed account.</param>
public sealed record ForgotPasswordRequest(string Email);
