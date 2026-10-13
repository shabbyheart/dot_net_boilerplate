using Boilerplate.Domain.Accounts;
using Boilerplate.Domain.Common;

namespace Boilerplate.UnitTests.Domain;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    [Fact]
    public void Issue_starts_an_active_family()
    {
        var tenantId = Guid.CreateVersion7();

        var token = RefreshToken.Issue("user-1", tenantId, "hash-1", Now, Lifetime);

        token.UserId.ShouldBe("user-1");
        token.TenantId.ShouldBe(tenantId);
        token.FamilyId.ShouldNotBe(Guid.Empty);
        token.ExpiresAt.ShouldBe(Now + Lifetime);
        token.IsActive(Now).ShouldBeTrue();
        token.IsActive(Now + Lifetime).ShouldBeFalse();
    }

    [Fact]
    public void Rotate_replaces_the_token_within_the_family()
    {
        var token = RefreshToken.Issue("user-1", tenantId: null, "hash-1", Now, Lifetime);
        var later = Now.AddHours(1);

        var next = token.Rotate("hash-2", later, Lifetime);

        token.RevokedAt.ShouldBe(later);
        token.ReplacedById.ShouldBe(next.Id);
        next.FamilyId.ShouldBe(token.FamilyId);
        next.UserId.ShouldBe(token.UserId);
        next.TenantId.ShouldBe(token.TenantId);
        next.TokenHash.ShouldBe("hash-2");
        next.ExpiresAt.ShouldBe(later + Lifetime);
        next.IsActive(later).ShouldBeTrue();
    }

    [Fact]
    public void Used_or_expired_tokens_cannot_be_rotated()
    {
        var used = RefreshToken.Issue("user-1", tenantId: null, "hash-1", Now, Lifetime);
        used.Rotate("hash-2", Now, Lifetime);
        var expired = RefreshToken.Issue("user-1", tenantId: null, "hash-3", Now, Lifetime);

        Should.Throw<DomainException>(() => used.Rotate("hash-4", Now, Lifetime));
        Should.Throw<DomainException>(() => expired.Rotate("hash-5", Now + Lifetime, Lifetime));
    }

    [Fact]
    public void Revoke_keeps_the_first_revocation_time()
    {
        var token = RefreshToken.Issue("user-1", tenantId: null, "hash-1", Now, Lifetime);

        token.Revoke(Now);
        token.Revoke(Now.AddMinutes(5));

        token.RevokedAt.ShouldBe(Now);
        token.IsActive(Now).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_user_id_is_required(string userId) =>
        Should.Throw<DomainException>(() => RefreshToken.Issue(userId, tenantId: null, "hash", Now, Lifetime));
}
