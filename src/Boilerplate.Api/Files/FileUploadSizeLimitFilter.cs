using Boilerplate.Application.Features.Files;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Boilerplate.Api.Files;

/// <summary>
/// Raises the request body limit for the upload endpoint only, to the configured maximum file size plus room for the
/// multipart envelope (ADR-0030). Every other endpoint keeps the global 1 MB limit (ADR-0024). Runs before model
/// binding reads the body.
/// </summary>
internal sealed class FileUploadSizeLimitFilter(FileSettings settings) : IResourceFilter
{
    // Boundaries, headers and the file name around the content.
    private const long MultipartOverhead = 64 * 1024;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var limit = settings.MaxFileSize + MultipartOverhead;

        // MVC's form reading turns Kestrel's own 413 into a 400 model error, so a declared length over the limit is
        // refused here, before anything reads the body. Bodies without a length still stop at the Kestrel limit (400).
        if (context.HttpContext.Request.ContentLength > limit)
        {
            throw new BadHttpRequestException(
                $"The request body is larger than the upload limit of {settings.MaxFileSize} bytes per file.",
                StatusCodes.Status413PayloadTooLarge);
        }

        var bodySize = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySize is { IsReadOnly: false })
        {
            bodySize.MaxRequestBodySize = limit;
        }

        context.HttpContext.Features.Set<IFormFeature>(new FormFeature(
            context.HttpContext.Request,
            new FormOptions { MultipartBodyLengthLimit = limit }));
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
