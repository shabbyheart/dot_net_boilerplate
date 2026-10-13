namespace Boilerplate.Application.Features.Accounts;

/// <param name="AccessToken">Send as <c>Authorization: Bearer ...</c>.</param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
/// <param name="AccessTokenExpiresAt">After this, use the refresh token to get a new pair.</param>
/// <param name="RefreshToken">Single use: every refresh returns a new one. Store it securely.</param>
/// <param name="RefreshTokenExpiresAt">After this, the user must sign in again.</param>
public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
