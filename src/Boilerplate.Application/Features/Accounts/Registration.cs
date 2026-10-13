namespace Boilerplate.Application.Features.Accounts;

/// <summary>The result of <see cref="IUserAccounts.RegisterAsync"/>.</summary>
/// <param name="UserId">The new or existing account.</param>
/// <param name="AlreadyConfirmed">The address belongs to a confirmed account, which was left unchanged.</param>
public sealed record Registration(string UserId, bool AlreadyConfirmed);
