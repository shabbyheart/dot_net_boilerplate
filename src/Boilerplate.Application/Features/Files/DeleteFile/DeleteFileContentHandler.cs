using Boilerplate.Application.Common.Messaging;
using Boilerplate.Application.Common.Storage;

namespace Boilerplate.Application.Features.Files.DeleteFile;

/// <summary>Delivers <see cref="DeleteFileContentMessage"/>. Idempotent: deleting missing content succeeds.</summary>
public sealed class DeleteFileContentHandler(IFileStorage storage) : IOutboxMessageHandler<DeleteFileContentMessage>
{
    public Task HandleAsync(DeleteFileContentMessage message, OutboxMessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return storage.DeleteAsync(message.StorageKey, cancellationToken);
    }
}
