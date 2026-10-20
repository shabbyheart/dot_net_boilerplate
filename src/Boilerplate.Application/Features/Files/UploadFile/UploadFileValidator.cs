using Boilerplate.Domain.Files;
using FluentValidation;

namespace Boilerplate.Application.Features.Files.UploadFile;

public sealed class UploadFileValidator : AbstractValidator<UploadFileRequest>
{
    public UploadFileValidator(FileSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RuleFor(request => request.FileName).NotEmpty();
        RuleFor(request => request.ContentType)
            .NotEmpty()
            .MaximumLength(StoredFile.ContentTypeMaxLength)
            .Must(settings.Allows)
            .WithMessage(request =>
                $"Files of type '{request.ContentType}' are not accepted. Allowed: {string.Join(", ", settings.EffectiveAllowedContentTypes)}.");
        RuleFor(request => request.Length)
            .GreaterThan(0)
            .WithMessage("The file must not be empty.")
            .LessThanOrEqualTo(settings.MaxFileSize)
            .WithMessage($"The file must be at most {settings.MaxFileSize} bytes.");
    }
}
