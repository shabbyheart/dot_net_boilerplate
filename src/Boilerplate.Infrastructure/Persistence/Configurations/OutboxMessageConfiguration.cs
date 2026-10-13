using Boilerplate.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Type).HasMaxLength(OutboxMessage.TypeMaxLength);

        // Free-form JSON whose size depends on the message type.
        builder.Property(message => message.Payload).HasColumnType("text");
        builder.Property(message => message.LastError).HasMaxLength(OutboxMessage.LastErrorMaxLength);

        // Serves the dispatcher's claim query; only pending rows are indexed, so it stays small.
        builder.HasIndex(message => message.AvailableAt)
            .HasFilter("processed_at IS NULL AND dead_lettered_at IS NULL")
            .HasDatabaseName("ix_outbox_messages_pending");
    }
}
