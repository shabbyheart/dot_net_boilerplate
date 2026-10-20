using Boilerplate.Application.Common.Exceptions;
using Boilerplate.Application.Common.Messaging;
using Boilerplate.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Application.Features.Files.DeleteFile;

public sealed class DeleteFileHandler(IAppDbContext db, Outbox outbox)
{
    /// <summary>Deletes the file; its content is deleted after the commit, through the outbox (ADR-0019, ADR-0030).</summary>
    /// <exception cref="NotFoundException">No file with this id exists in the current tenant.</exception>
    public async Task HandleAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var file = await db.Files.SingleOrDefaultAsync(f => f.Id == fileId, cancellationToken)
            ?? throw new NotFoundException("File", fileId);

        db.Files.Remove(file);
        outbox.Enqueue(new DeleteFileContentMessage(file.StorageKey));
        await db.SaveChangesAsync(cancellationToken);
    }
}
