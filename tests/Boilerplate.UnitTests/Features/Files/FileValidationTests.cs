using System.ComponentModel.DataAnnotations;
using Boilerplate.Application.Features.Files;
using Boilerplate.Application.Features.Files.UploadFile;

namespace Boilerplate.UnitTests.Features.Files;

public sealed class FileValidationTests
{
    private static readonly FileSettings Settings = new() { MaxFileSize = 100 };

    [Theory]
    [InlineData("image/png", true)]
    [InlineData("IMAGE/PNG", true)]
    [InlineData("text/html", false)]
    [InlineData("application/x-msdownload", false)]
    [InlineData("", false)]
    public void Only_allowed_content_types_are_accepted(string contentType, bool valid) =>
        Validate(new UploadFileRequest("a.bin", contentType, 10, Stream.Null)).ShouldBe(valid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Length_must_be_within_the_limit(long length, bool valid) =>
        Validate(new UploadFileRequest("a.txt", "text/plain", length, Stream.Null)).ShouldBe(valid);

    [Fact]
    public void A_file_name_is_required() =>
        Validate(new UploadFileRequest("", "text/plain", 10, Stream.Null)).ShouldBeFalse();

    [Fact]
    public void Configured_content_types_replace_the_defaults()
    {
        var settings = new FileSettings { AllowedContentTypes = ["application/zip"] };

        settings.Allows("application/zip").ShouldBeTrue();
        settings.Allows("image/png").ShouldBeFalse();
    }

    [Fact]
    public void Settings_reject_an_empty_allowlist()
    {
        var settings = new FileSettings { AllowedContentTypes = [] };

        settings.Validate(new ValidationContext(settings)).ShouldNotBeEmpty();
        new FileSettings().Validate(new ValidationContext(new FileSettings())).ShouldBeEmpty();
    }

    private static bool Validate(UploadFileRequest request) =>
        new UploadFileValidator(Settings).Validate(request).IsValid;
}
