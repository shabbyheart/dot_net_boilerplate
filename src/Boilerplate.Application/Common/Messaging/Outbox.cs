using System.Text.Json;
using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Tenancy;

namespace Boilerplate.Application.Common.Messaging;

/// <summary>
/// Records side effects for later delivery (ADR-0019). <see cref="Enqueue{TMessage}"/> only adds a row to the
/// current unit of work: it is saved by the use case's own <c>SaveChangesAsync</c>, together with the business change.
/// </summary>
public sealed class Outbox(
    IAppDbContext db,
    ITenantContext tenantContext,
    OutboxMessageTypes messageTypes,
    TimeProvider timeProvider)
{
    internal static readonly JsonSerializerOptions SerializerOptions = JsonSerializerOptions.Web;

    public void Enqueue<TMessage>(TMessage message)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);

        db.OutboxMessages.Add(OutboxMessage.Create(
            messageTypes.NameOf(typeof(TMessage)),
            JsonSerializer.Serialize(message, SerializerOptions),
            tenantContext.TenantId,
            timeProvider.GetUtcNow()));
    }
}
