using Boilerplate.Application.Common.Pagination;
using Boilerplate.Application.Features.Projects.CreateProject;
using Boilerplate.Application.Features.Projects.ListProjects;
using Boilerplate.Application.Features.Projects.UpdateProject;
using Boilerplate.Domain.Projects;

namespace Boilerplate.UnitTests.Features.Projects;

public sealed class ProjectValidatorTests
{
    [Fact]
    public void Valid_create_request_passes() =>
        new CreateProjectValidator().Validate(new CreateProjectRequest("Website", "Public site")).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_a_name(string name) =>
        ErrorProperties(new CreateProjectValidator().Validate(new CreateProjectRequest(name, null))).ShouldBe(["Name"]);

    [Fact]
    public void Create_limits_name_and_description_length()
    {
        var request = new CreateProjectRequest(
            new string('n', Project.NameMaxLength + 1),
            new string('d', Project.DescriptionMaxLength + 1));

        ErrorProperties(new CreateProjectValidator().Validate(request)).ShouldBe(["Description", "Name"], ignoreOrder: true);
    }

    [Fact]
    public void Update_requires_a_name() =>
        ErrorProperties(new UpdateProjectValidator().Validate(new UpdateProjectRequest("", null))).ShouldBe(["Name"]);

    [Fact]
    public void List_defaults_are_valid() =>
        new ListProjectsValidator().Validate(new ListProjectsRequest()).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData(0, 20, "Page")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, PageRequest.MaxPageSize + 1, "PageSize")]
    public void List_rejects_pages_out_of_range(int page, int pageSize, string property) =>
        ErrorProperties(new ListProjectsValidator().Validate(new ListProjectsRequest { Page = page, PageSize = pageSize }))
            .ShouldBe([property]);

    [Fact]
    public void List_accepts_the_maximum_page_size() =>
        new ListProjectsValidator().Validate(new ListProjectsRequest { PageSize = PageRequest.MaxPageSize })
            .IsValid.ShouldBeTrue();

    private static string[] ErrorProperties(FluentValidation.Results.ValidationResult result) =>
        result.Errors.Select(error => error.PropertyName).Distinct().ToArray();
}
