using Boilerplate.Domain.Common;
using Boilerplate.Domain.Files;

namespace Boilerplate.UnitTests.Domain;

public sealed class StoredFileTests
{
    [Theory]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData("  report.pdf  ", "report.pdf")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData(@"C:\Users\someone\report.pdf", "report.pdf")]
    [InlineData("re\"port\r\n.pdf", "report.pdf")]
    [InlineData("", StoredFile.DefaultFileName)]
    [InlineData("..", StoredFile.DefaultFileName)]
    [InlineData("folder/", StoredFile.DefaultFileName)]
    [InlineData(null, StoredFile.DefaultFileName)]
    public void File_names_keep_only_a_safe_last_segment(string? uploaded, string expected) =>
        StoredFile.NormalizeFileName(uploaded).ShouldBe(expected);

    [Fact]
    public void Long_file_names_are_shortened_without_splitting_characters()
    {
        StoredFile.NormalizeFileName(new string('a', 300)).Length.ShouldBe(StoredFile.FileNameMaxLength);

        // An emoji is two UTF-16 units; it must not be cut in half at the limit.
        var name = new string('a', StoredFile.FileNameMaxLength - 1) + "😀";
        StoredFile.NormalizeFileName(name).ShouldBe(new string('a', StoredFile.FileNameMaxLength - 1));
    }

    [Fact]
    public void Create_keeps_the_description_and_derives_the_storage_key()
    {
        var file = StoredFile.Create("Report.PDF", " Application/PDF ", 42);

        file.FileName.ShouldBe("Report.PDF");
        file.ContentType.ShouldBe("application/pdf");
        file.Length.ShouldBe(42);
        file.StorageKey.ShouldBe(file.Id.ToString("N"));
    }

    [Fact]
    public void Empty_files_are_rejected() =>
        Should.Throw<DomainException>(() => StoredFile.Create("a.txt", "text/plain", 0));
}
