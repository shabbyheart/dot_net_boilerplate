using System.ComponentModel.DataAnnotations;

namespace Boilerplate.Infrastructure.Housekeeping;

/// <summary>Retention cleanup settings (<c>Housekeeping</c> section, all optional; ADR-0026).</summary>
public sealed class HousekeepingOptions
{
    public const string SectionName = "Housekeeping";

    /// <summary>Whether this instance runs the cleanup. Running it on several instances is safe.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Time between cleanup rounds; the first round runs at startup.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Rows deleted per statement, so no single statement locks or scans a large part of a table.</summary>
    [Range(1, 10_000)]
    public int BatchSize { get; set; } = 1000;

    /// <summary>How long refresh tokens, one-time tokens and invitations are kept after they expire.</summary>
    [Range(typeof(TimeSpan), "00:00:00", "365.00:00:00")]
    public TimeSpan ExpiredTokenRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>How long delivered outbox messages (already without payload) are kept after delivery.</summary>
    [Range(typeof(TimeSpan), "00:00:00", "365.00:00:00")]
    public TimeSpan ProcessedOutboxRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>How long audit log entries are kept (ADR-0028).</summary>
    [Range(typeof(TimeSpan), "1.00:00:00", "3650.00:00:00")]
    public TimeSpan AuditLogRetention { get; set; } = TimeSpan.FromDays(365);
}
