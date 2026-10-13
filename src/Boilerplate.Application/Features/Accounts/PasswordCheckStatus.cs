namespace Boilerplate.Application.Features.Accounts;

public enum PasswordCheckStatus
{
    /// <summary>Unknown address or wrong password.</summary>
    Failed,

    /// <summary>Too many failed attempts; the password was not checked.</summary>
    LockedOut,

    /// <summary>The password was correct, but the address is not confirmed yet.</summary>
    EmailNotConfirmed,

    Succeeded,
}
