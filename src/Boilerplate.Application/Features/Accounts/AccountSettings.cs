using System.ComponentModel.DataAnnotations;

namespace Boilerplate.Application.Features.Accounts;

/// <summary>Local account settings (<c>Accounts</c> section; ADR-0025). Token signing settings are in <c>Accounts:Tokens</c>.</summary>
public sealed class AccountSettings : IValidatableObject
{
    public const string SectionName = "Accounts";

    /// <summary>Whether the API offers local accounts. Off by default: an external identity provider is the default (ADR-0016).</summary>
    public bool Enabled { get; set; }

    /// <summary>The client page that confirms an e-mail address; the e-mail links to it with a <c>token</c> query parameter.</summary>
    public string? EmailConfirmationUrl { get; set; }

    /// <summary>The client page that sets a new password; the e-mail links to it with a <c>token</c> query parameter.</summary>
    public string? PasswordResetUrl { get; set; }

    /// <summary>How long a refresh token can be used. Every refresh issues a new one with a new lifetime.</summary>
    [Range(typeof(TimeSpan), "01:00:00", "90.00:00:00")]
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);

    [Range(typeof(TimeSpan), "00:10:00", "7.00:00:00")]
    public TimeSpan EmailConfirmationLifetime { get; set; } = TimeSpan.FromDays(1);

    [Range(typeof(TimeSpan), "00:05:00", "1.00:00:00")]
    public TimeSpan PasswordResetLifetime { get; set; } = TimeSpan.FromHours(1);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
        {
            yield break;
        }

        if (!IsAbsoluteHttpUrl(EmailConfirmationUrl))
        {
            yield return new ValidationResult(
                $"{SectionName}:{nameof(EmailConfirmationUrl)} must be an absolute http(s) URL when accounts are enabled.",
                [nameof(EmailConfirmationUrl)]);
        }

        if (!IsAbsoluteHttpUrl(PasswordResetUrl))
        {
            yield return new ValidationResult(
                $"{SectionName}:{nameof(PasswordResetUrl)} must be an absolute http(s) URL when accounts are enabled.",
                [nameof(PasswordResetUrl)]);
        }
    }

    private static bool IsAbsoluteHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
