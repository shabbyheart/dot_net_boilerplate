using System.ComponentModel.DataAnnotations;
using MailKit.Security;

namespace Boilerplate.Infrastructure.Email;

/// <summary>SMTP settings (<c>Email:Smtp</c> section), validated at startup (ADR-0007, ADR-0019).</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    /// <summary>Transport security. <c>StartTls</c> by default; <c>None</c> only for local capture tools such as Mailpit.</summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;

    /// <summary>Optional. A secret: supply it through user secrets or environment variables.</summary>
    public string? Username { get; set; }

    /// <summary>Optional. A secret: supply it through user secrets or environment variables.</summary>
    public string? Password { get; set; }

    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    public string? FromName { get; set; }
}
