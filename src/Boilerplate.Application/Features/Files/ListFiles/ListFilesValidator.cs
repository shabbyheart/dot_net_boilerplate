using Boilerplate.Application.Common.Pagination;
using FluentValidation;

namespace Boilerplate.Application.Features.Files.ListFiles;

public sealed class ListFilesValidator : AbstractValidator<ListFilesRequest>
{
    public ListFilesValidator()
    {
        Include(new PageRequestValidator());
    }
}
