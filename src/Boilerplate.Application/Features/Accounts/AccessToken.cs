namespace Boilerplate.Application.Features.Accounts;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
