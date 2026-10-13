using Boilerplate.Application.Common.Pagination;
using FluentValidation;

namespace Boilerplate.Application.Features.Projects.ListProjects;

public sealed class ListProjectsValidator : AbstractValidator<ListProjectsRequest>
{
    public ListProjectsValidator()
    {
        Include(new PageRequestValidator());
    }
}
