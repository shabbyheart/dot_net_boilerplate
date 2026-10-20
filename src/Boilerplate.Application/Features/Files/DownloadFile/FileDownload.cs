namespace Boilerplate.Application.Features.Files.DownloadFile;

/// <summary>A file's content to send to the client. The caller disposes <paramref name="Content"/>.</summary>
public sealed record FileDownload(Stream Content, string ContentType, string FileName, long Length);
