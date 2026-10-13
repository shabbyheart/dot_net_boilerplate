using Boilerplate.Domain.Projects;
using FluentValidation;

namespace Boilerplate.Application.Features.Projects.CreateProject;

public sealed class CreateProjectValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(Project.NameMaxLength);
        RuleFor(request => request.Description).MaximumLength(Project.DescriptionMaxLength);
    }
}
