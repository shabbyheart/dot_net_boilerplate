using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Common;

namespace Boilerplate.UnitTests.Domain;

public sealed class InvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    [Fact]
    public void Issue_normalizes_the_address_and_sets_the_expiry()
    {
        var role = Role.Create("Readers");

        var invitation = Invitation.Issue("  Someone@Example.COM ", role, "hash", Now, Lifetime);

        invitation.Email.ShouldBe("someone@example.com");
        invitation.RoleId.ShouldBe(role.Id);
        invitation.ExpiresAt.ShouldBe(Now + Lifetime);
        invitation.IsPending(Now).ShouldBeTrue();
    }

    [Fact]
    public void An_invitation_can_be_accepted_once()
    {
        var invitation = Invitation.Issue("a@example.com", Role.Create("Readers"), "hash", Now, Lifetime);

        invitation.Accept("user-1", Now.AddDays(1));

        invitation.AcceptedBy.ShouldBe("user-1");
        invitation.AcceptedAt.ShouldBe(Now.AddDays(1));
        Should.Throw<DomainException>(() => invitation.Accept("user-2", Now.AddDays(2))).Message.ShouldBe(Invitation.InvalidMessage);
    }

    [Fact]
    public void Expired_or_revoked_invitations_cannot_be_accepted()
    {
        var expired = Invitation.Issue("a@example.com", Role.Create("Readers"), "hash-1", Now, Lifetime);
        var revoked = Invitation.Issue("a@example.com", Role.Create("Readers"), "hash-2", Now, Lifetime);
        revoked.Revoke(Now);

        Should.Throw<DomainException>(() => expired.Accept("user-1", Now + Lifetime));
        Should.Throw<DomainException>(() => revoked.Accept("user-1", Now));
    }

    [Fact]
    public void Revoking_an_accepted_invitation_changes_nothing()
    {
        var invitation = Invitation.Issue("a@example.com", Role.Create("Readers"), "hash", Now, Lifetime);
        invitation.Accept("user-1", Now);

        invitation.Revoke(Now.AddMinutes(1));

        invitation.RevokedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_address_is_required(string email) =>
        Should.Throw<DomainException>(() => Invitation.Issue(email, Role.Create("Readers"), "hash", Now, Lifetime));
}
