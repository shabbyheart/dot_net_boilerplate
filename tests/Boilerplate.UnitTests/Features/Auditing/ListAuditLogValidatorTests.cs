using Boilerplate.Application.Features.Auditing.ListAuditLog;
using Boilerplate.Domain.Auditing;
using Boilerplate.Domain.Common;

namespace Boilerplate.UnitTests.Features.Auditing;

public sealed class ListAuditLogValidatorTests
{
    private readonly ListAuditLogValidator _validator = new();

    [Fact]
    public void Filters_are_optional() =>
        _validator.Validate(new ListAuditLogRequest()).IsValid.ShouldBeTrue();

    [Fact]
    public void Filters_are_bounded()
    {
        _validator.Validate(new ListAuditLogRequest { Action = new string('a', AuditLogEntry.ActionMaxLength + 1) })
            .IsValid.ShouldBeFalse();
        _validator.Validate(new ListAuditLogRequest { ActorId = new string('u', IAuditable.UserIdMaxLength + 1) })
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Paging_limits_apply() =>
        _validator.Validate(new ListAuditLogRequest { PageSize = 0 }).IsValid.ShouldBeFalse();
}
