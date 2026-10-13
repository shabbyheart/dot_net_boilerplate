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

    /// <summary>
    /// Account requests (sign-in, registration, password reset) allowed per client IP in each <see cref="AccountsWindow"/>,
    /// in addition to <see cref="PermitLimit"/>. Slows down password guessing across many accounts (ADR-0025).
    /// </summary>
    [Range(1, 1_000_000)]
    public int AccountsPermitLimit { get; set; } = 10;

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan AccountsWindow { get; set; } = TimeSpan.FromMinutes(1);
}
