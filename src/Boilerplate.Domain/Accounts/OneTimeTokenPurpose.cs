namespace Boilerplate.Domain.Accounts;

/// <summary>What a <see cref="OneTimeToken"/> may be used for. Stored by name: never rename a value.</summary>
public enum OneTimeTokenPurpose
{
    EmailConfirmation = 1,
    PasswordReset = 2,
}
