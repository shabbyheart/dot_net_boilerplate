namespace Boilerplate.Application.Features.Accounts;

public enum PasswordCheckStatus
{
    /// <summary>Unknown address, wrong password, or wrong second factor.</summary>
    Failed,

    /// <summary>Too many failed attempts; the password was not checked.</summary>
    LockedOut,

    /// <summary>The password was correct, but the address is not confirmed yet.</summary>
    EmailNotConfirmed,

    /// <summary>The password was correct, but the account needs a second factor and none was given (ADR-0031).</summary>
    TwoFactorRequired,

    Succeeded,
}
