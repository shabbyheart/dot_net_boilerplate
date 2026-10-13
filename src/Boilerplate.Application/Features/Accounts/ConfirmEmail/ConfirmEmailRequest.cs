namespace Boilerplate.Application.Features.Accounts.ConfirmEmail;

/// <param name="Token">The <c>token</c> parameter of the link in the confirmation e-mail.</param>
public sealed record ConfirmEmailRequest(string Token);
