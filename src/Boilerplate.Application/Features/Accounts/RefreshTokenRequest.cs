namespace Boilerplate.Application.Features.Accounts;

/// <param name="RefreshToken">The refresh token from the last sign-in or refresh.</param>
public sealed record RefreshTokenRequest(string RefreshToken);
