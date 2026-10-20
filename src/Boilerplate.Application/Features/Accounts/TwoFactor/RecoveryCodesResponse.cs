namespace Boilerplate.Application.Features.Accounts.TwoFactor;

/// <summary>New recovery codes. They are shown only now; each works once instead of an authenticator code.</summary>
public sealed record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);
