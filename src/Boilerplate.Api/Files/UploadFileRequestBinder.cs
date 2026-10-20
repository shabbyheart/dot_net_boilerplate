using Boilerplate.Application.Features.Files.UploadFile;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Boilerplate.Api.Files;

/// <summary>
/// Binds <see cref="UploadFileRequest"/> from the multipart field <c>file</c> (ADR-0030), so Application code never sees
/// <c>IFormFile</c> and the request is validated by <c>ValidationFilter</c> like any other (ADR-0014).
/// </summary>
internal sealed class UploadFileRequestBinder : IModelBinder
{
    public const string FieldName = "file";

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var request = bindingContext.HttpContext.Request;
        if (!request.HasFormContentType)
        {
            Fail(bindingContext, "Send the file as multipart/form-data.");
            return;
        }

        var form = await request.ReadFormAsync(bindingContext.HttpContext.RequestAborted);
        var file = form.Files.GetFile(FieldName);
        if (file is null)
        {
            Fail(bindingContext, $"The multipart field '{FieldName}' with the file is required.");
            return;
        }

        // The form file's stream is disposed with the request.
        bindingContext.Result = ModelBindingResult.Success(
            new UploadFileRequest(file.FileName, file.ContentType ?? string.Empty, file.Length, file.OpenReadStream()));
    }

    private static void Fail(ModelBindingContext bindingContext, string message)
    {
        bindingContext.ModelState.AddModelError(FieldName, message);
        bindingContext.Result = ModelBindingResult.Failed();
    }
}
