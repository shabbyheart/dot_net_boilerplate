using Boilerplate.Domain.Auditing;
using Boilerplate.Domain.Common;

namespace Boilerplate.UnitTests.Domain;

public sealed class AuditLogEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Record_keeps_what_happened()
    {
        var targetId = Guid.NewGuid();

        var entry = AuditLogEntry.Record(Now, "user-1", "role.created", targetId, "Created role 'Editors'.");

        entry.OccurredAt.ShouldBe(Now);
        entry.ActorId.ShouldBe("user-1");
        entry.Action.ShouldBe("role.created");
        entry.TargetId.ShouldBe(targetId);
        entry.Description.ShouldBe("Created role 'Editors'.");
    }

    [Fact]
    public void Commands_record_without_an_actor() =>
        AuditLogEntry.Record(Now, actorId: null, "tenant.created", Guid.NewGuid(), "Created tenant.").ActorId.ShouldBeNull();

    [Fact]
    public void Long_descriptions_are_shortened_instead_of_failing_the_change()
    {
        var entry = AuditLogEntry.Record(Now, "user-1", "role.updated", Guid.NewGuid(), new string('x', 5000));

        entry.Description.Length.ShouldBe(AuditLogEntry.DescriptionMaxLength);
        entry.Description.ShouldEndWith("…");
    }

    [Fact]
    public void Action_actor_and_target_are_required_and_bounded()
    {
        Should.Throw<DomainException>(() => AuditLogEntry.Record(Now, "user-1", " ", Guid.NewGuid(), "d"));
        Should.Throw<DomainException>(() =>
            AuditLogEntry.Record(Now, "user-1", new string('a', AuditLogEntry.ActionMaxLength + 1), Guid.NewGuid(), "d"));
        Should.Throw<DomainException>(() =>
            AuditLogEntry.Record(Now, new string('u', IAuditable.UserIdMaxLength + 1), "role.created", Guid.NewGuid(), "d"));
        Should.Throw<DomainException>(() => AuditLogEntry.Record(Now, "user-1", "role.created", Guid.Empty, "d"));
    }
}
