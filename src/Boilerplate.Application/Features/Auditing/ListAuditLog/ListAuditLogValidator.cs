using Boilerplate.Application.Common.Pagination;
using Boilerplate.Domain.Auditing;
using Boilerplate.Domain.Common;
using FluentValidation;

namespace Boilerplate.Application.Features.Auditing.ListAuditLog;

public sealed class ListAuditLogValidator : AbstractValidator<ListAuditLogRequest>
{
    public ListAuditLogValidator()
    {
        Include(new PageRequestValidator());
        RuleFor(request => request.Action).MaximumLength(AuditLogEntry.ActionMaxLength);
        RuleFor(request => request.ActorId).MaximumLength(IAuditable.UserIdMaxLength);
    }
}
