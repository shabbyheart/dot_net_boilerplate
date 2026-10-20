using Boilerplate.Application.Common.Security;
using FluentValidation;

namespace Boilerplate.Application.Features.Accounts;

/// <summary>Validation rules shared by the account use cases (ADR-0014, ADR-0025).</summary>
public static class AccountRules
{
    public const int EmailMaxLength = 256;

    /// <summary>Length is the only password rule (NIST SP 800-63B); Identity is configured with the same minimum.</summary>
    public const int PasswordMinLength = 12;

    /// <summary>Bounds the hashing work per request.</summary>
    public const int PasswordMaxLength = 128;

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().EmailAddress().MaximumLength(EmailMaxLength);

    /// <summary>For a password being set.</summary>
    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Length(PasswordMinLength, PasswordMaxLength);

    /// <summary>For a password being checked: rules may have changed since it was set, so only bound its size.</summary>
    public static IRuleBuilderOptions<T, string> ExistingPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(PasswordMaxLength);

    public static IRuleBuilderOptions<T, string> ValidToken<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(SecureToken.MaxLength);

    /// <summary>Six digits from an authenticator app; apps often show them with a space in the middle (ADR-0031).</summary>
    public static IRuleBuilderOptions<T, string> AuthenticatorCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(16).Matches("^[0-9 -]+$").WithMessage("Enter the code from your authenticator app.");

    public static IRuleBuilderOptions<T, string> RecoveryCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(TwoFactor.RecoveryCodes.MaxLength);
}
