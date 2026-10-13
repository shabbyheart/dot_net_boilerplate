using System.ComponentModel.DataAnnotations;

namespace Boilerplate.Api.Hardening;

/// <summary>Per-caller request budget (<c>RateLimiting</c> section; ADR-0024).</summary>
public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>Requests allowed per caller in each <see cref="Window"/>.</summary>
    [Range(1, 1_000_000)]
    public int PermitLimit { get; set; } = 100;

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(10);
}
