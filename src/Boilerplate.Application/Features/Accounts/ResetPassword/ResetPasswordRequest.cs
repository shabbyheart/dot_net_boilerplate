namespace Boilerplate.Application.Features.Accounts.ResetPassword;

/// <param name="Token">The <c>token</c> parameter of the link in the reset e-mail.</param>
/// <param name="NewPassword">12 to 128 characters.</param>
public sealed record ResetPasswordRequest(string Token, string NewPassword);
