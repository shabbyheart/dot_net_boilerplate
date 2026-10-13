using System.Collections.Concurrent;
using Boilerplate.Application.Common.Messaging;
using Boilerplate.Application.Common.Tenancy;

namespace Boilerplate.IntegrationTests.Messaging;

/// <summary>A test-only outbox message, so dispatching can be verified without an external service.</summary>
public sealed record ProbeMessage(string Key)
{
    public const string OutboxType = "test.probe";
}

/// <summary>What <see cref="ProbeHandler"/> saw, and which keys it should fail on.</summary>
public sealed class ProbeRecorder
{
    public ConcurrentQueue<(string Key, Guid? Tenant, int Attempt)> Deliveries { get; } = new();

    /// <summary>Keys to fail on, with how many more failures remain (int.MaxValue = always).</summary>
    public ConcurrentDictionary<string, int> Failures { get; } = new(StringComparer.Ordinal);
}

internal sealed class ProbeHandler(ProbeRecorder recorder, ITenantContext tenantContext) : IOutboxMessageHandler<ProbeMessage>
{
    public Task HandleAsync(ProbeMessage message, OutboxMessageContext context, CancellationToken cancellationToken)
    {
        if (recorder.Failures.TryGetValue(message.Key, out var remaining) && remaining > 0)
        {
            recorder.Failures[message.Key] = remaining == int.MaxValue ? remaining : remaining - 1;
            throw new InvalidOperationException($"Probe failure for {message.Key}");
        }

        recorder.Deliveries.Enqueue((message.Key, tenantContext.TenantId, context.Attempt));
        return Task.CompletedTask;
    }
}
