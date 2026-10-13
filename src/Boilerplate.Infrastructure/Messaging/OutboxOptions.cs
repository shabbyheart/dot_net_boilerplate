using System.ComponentModel.DataAnnotations;

namespace Boilerplate.Infrastructure.Messaging;

/// <summary>Outbox dispatcher settings (<c>Outbox</c> section, all optional; ADR-0019).</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Whether this instance dispatches messages. Every instance can enqueue regardless.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often to look for pending messages.</summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:05:00")]
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Messages claimed per round.</summary>
    [Range(1, 500)]
    public int BatchSize { get; set; } = 20;

    /// <summary>How long a claimed message is reserved; after a crash it becomes available again after this time.</summary>
    [Range(typeof(TimeSpan), "00:00:10", "01:00:00")]
    public TimeSpan Lease { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Attempts before a message is dead-lettered.</summary>
    [Range(1, 100)]
    public int MaxAttempts { get; set; } = 8;
}
