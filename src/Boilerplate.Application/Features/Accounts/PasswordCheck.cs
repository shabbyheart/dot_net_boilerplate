namespace Boilerplate.Application.Features.Accounts;

/// <summary>The result of <see cref="IUserAccounts.CheckPasswordAsync"/>.</summary>
/// <param name="Status">The outcome.</param>
/// <param name="UserId">Set when the password was correct.</param>
public sealed record PasswordCheck(PasswordCheckStatus Status, string? UserId = null);
