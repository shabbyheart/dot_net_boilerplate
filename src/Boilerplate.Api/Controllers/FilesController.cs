using Boilerplate.Api.Authorization;
using Boilerplate.Api.Files;
using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Features.Files;
using Boilerplate.Application.Features.Files.DeleteFile;
using Boilerplate.Application.Features.Files.DownloadFile;
using Boilerplate.Application.Features.Files.GetFile;
using Boilerplate.Application.Features.Files.ListFiles;
using Boilerplate.Application.Features.Files.UploadFile;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

/// <summary>Files of the caller's tenant (ADR-0030): descriptions as JSON, content as attachments.</summary>
[ApiController]
[Route("api/v1/files")]
public sealed class FilesController : ControllerBase
{
    private const string GetFileRoute = "GetFile";

    /// <summary>Lists files, newest first.</summary>
    [HttpGet]
    [RequirePermission(FilePermissions.Read)]
    [ProducesResponseType<PagedResponse<FileResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResponse<FileResponse>> List(
        [FromQuery] ListFilesRequest request,
        [FromServices] ListFilesHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, cancellationToken);

    /// <summary>Gets a file's description.</summary>
    [HttpGet("{id:guid}", Name = GetFileRoute)]
    [RequirePermission(FilePermissions.Read)]
    [ProducesResponseType<FileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<FileResponse> Get(Guid id, [FromServices] GetFileHandler handler, CancellationToken cancellationToken) =>
        handler.HandleAsync(id, cancellationToken);

    /// <summary>Downloads a file's content, always as an attachment.</summary>
    [HttpGet("{id:guid}/content")]
    [RequirePermission(FilePermissions.Read)]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, "application/octet-stream")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<FileStreamResult> Download(Guid id, [FromServices] DownloadFileHandler handler, CancellationToken cancellationToken)
    {
        var download = await handler.HandleAsync(id, cancellationToken);

        // A download name makes the response an attachment; with nosniff, browsers never render it in this origin.
        return File(download.Content, download.ContentType, download.FileName);
    }

    /// <summary>
    /// Uploads a file as <c>multipart/form-data</c> in the field <c>file</c>. Only this endpoint accepts bodies larger
    /// than 1 MB, up to the configured maximum file size.
    /// </summary>
    [HttpPost]
    [RequirePermission(FilePermissions.Manage)]
    [TypeFilter<FileUploadSizeLimitFilter>]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<FileResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<CreatedAtRouteResult> Upload(
        [ModelBinder<UploadFileRequestBinder>(Name = UploadFileRequestBinder.FieldName)] UploadFileRequest request,
        [FromServices] UploadFileHandler handler,
        CancellationToken cancellationToken)
    {
        var file = await handler.HandleAsync(request, cancellationToken);
        return CreatedAtRoute(GetFileRoute, new { id = file.Id }, file);
    }

    /// <summary>Deletes a file. Its content is removed shortly after, in the background.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(FilePermissions.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Delete(Guid id, [FromServices] DeleteFileHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return NoContent();
    }
}
