using System.ComponentModel.DataAnnotations;

namespace Boilerplate.Application.Features.Invitations;

/// <summary>Invitation settings (<c>Invitations</c> section, all optional; ADR-0027).</summary>
public sealed class InvitationSettings : IValidatableObject
{
    public const string SectionName = "Invitations";

    /// <summary>
    /// The client page that accepts an invitation; the e-mail links to it with a <c>token</c> query parameter.
    /// Without it, creating invitations (and tenants, which invite their first administrator) answers 409.
    /// </summary>
    public string? AcceptUrl { get; set; }

    /// <summary>How long an invitation can be accepted.</summary>
    [Range(typeof(TimeSpan), "01:00:00", "30.00:00:00")]
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(7);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AcceptUrl is not null && !IsAbsoluteHttpUrl(AcceptUrl))
        {
            yield return new ValidationResult(
                $"{SectionName}:{nameof(AcceptUrl)} must be an absolute http(s) URL.",
                [nameof(AcceptUrl)]);
        }
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
