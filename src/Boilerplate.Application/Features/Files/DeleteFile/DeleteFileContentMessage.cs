namespace Boilerplate.Application.Features.Files.DeleteFile;

/// <summary>Deletes a deleted file's content after the deletion committed (ADR-0030).</summary>
public sealed record DeleteFileContentMessage(string StorageKey)
{
    /// <summary>The stable outbox type name. Never change it once messages exist.</summary>
    public const string OutboxType = "files.delete-content";
}
