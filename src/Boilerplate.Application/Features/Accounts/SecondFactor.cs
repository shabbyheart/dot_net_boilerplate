namespace Boilerplate.Application.Features.Accounts;

/// <summary>Proof of the second factor (ADR-0031): an authenticator code, or the hash of a recovery code.</summary>
/// <param name="Code">The code shown by the authenticator app.</param>
/// <param name="RecoveryCodeHash">The hash of a recovery code (<see cref="TwoFactor.RecoveryCodes.Hash"/>); never the code itself.</param>
public sealed record SecondFactor(string? Code, string? RecoveryCodeHash)
{
    /// <summary>The proof in a request, or <see langword="null"/> when it has none.</summary>
    public static SecondFactor? From(string? code, string? recoveryCode) =>
        !string.IsNullOrWhiteSpace(code) ? new SecondFactor(code, null)
        : !string.IsNullOrWhiteSpace(recoveryCode) ? new SecondFactor(null, TwoFactor.RecoveryCodes.Hash(recoveryCode))
        : null;
}
