using Boilerplate.Application.Features.Accounts;
using Microsoft.Extensions.Options;

namespace Boilerplate.Infrastructure.Accounts;

/// <summary>Fails startup when accounts are enabled without safe token settings (ADR-0007, ADR-0025).</summary>
internal sealed class AccessTokenOptionsValidation(IOptions<AccountSettings> accounts) : IValidateOptions<AccessTokenOptions>
{
    private static readonly TimeSpan MinimumLifetime = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaximumLifetime = TimeSpan.FromHours(1);

    public ValidateOptionsResult Validate(string? name, AccessTokenOptions options)
    {
        if (!accounts.Value.Enabled)
        {
            return ValidateOptionsResult.Skip;
        }

        const string Section = AccessTokenOptions.SectionName;
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add($"{Section}:Issuer is required when accounts are enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add($"{Section}:Audience is required when accounts are enabled.");
        }

        // Key values are never part of the message.
        if (!AccessTokenOptions.IsValidKey(options.SigningKey))
        {
            failures.Add($"{Section}:SigningKey must be base64 for at least {AccessTokenOptions.MinimumKeyBytes} random bytes.");
        }

        if (options.PreviousSigningKeys.Any(key => !AccessTokenOptions.IsValidKey(key)))
        {
            failures.Add($"{Section}:PreviousSigningKeys must each be base64 for at least {AccessTokenOptions.MinimumKeyBytes} bytes.");
        }

        if (options.Lifetime < MinimumLifetime || options.Lifetime > MaximumLifetime)
        {
            failures.Add($"{Section}:Lifetime must be between {MinimumLifetime} and {MaximumLifetime}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
