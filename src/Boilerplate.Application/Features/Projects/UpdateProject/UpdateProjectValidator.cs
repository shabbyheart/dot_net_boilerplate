using Boilerplate.Domain.Projects;
using FluentValidation;

namespace Boilerplate.Application.Features.Projects.UpdateProject;

public sealed class UpdateProjectValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(Project.NameMaxLength);
        RuleFor(request => request.Description).MaximumLength(Project.DescriptionMaxLength);
    }
}
