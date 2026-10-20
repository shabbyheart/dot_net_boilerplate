namespace Boilerplate.Application.Features.Accounts.TwoFactor;

/// <summary>A new authenticator key. Two-factor sign-in stays off until it is enabled with a code from this key.</summary>
/// <param name="SharedKey">The key (base32) for typing into an authenticator app.</param>
/// <param name="AuthenticatorUri">An <c>otpauth://</c> URI to show as a QR code.</param>
public sealed record AuthenticatorSetupResponse(string SharedKey, string AuthenticatorUri);
