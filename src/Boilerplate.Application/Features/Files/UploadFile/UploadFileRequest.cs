namespace Boilerplate.Application.Features.Files.UploadFile;

/// <summary>An uploaded file. The API binds it from the multipart field <c>file</c>.</summary>
/// <param name="FileName">The name the client sent; normalized before it is stored.</param>
/// <param name="ContentType">The media type the client declared.</param>
/// <param name="Length">The content length in bytes.</param>
/// <param name="Content">The content, read once by the handler.</param>
public sealed record UploadFileRequest(string FileName, string ContentType, long Length, Stream Content);
