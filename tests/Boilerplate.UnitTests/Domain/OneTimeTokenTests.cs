using Boilerplate.Domain.Accounts;
using Boilerplate.Domain.Common;

namespace Boilerplate.UnitTests.Domain;

public sealed class OneTimeTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    [Fact]
    public void A_token_can_be_redeemed_once()
    {
        var token = OneTimeToken.Issue("user-1", OneTimeTokenPurpose.PasswordReset, "hash", Now, Lifetime);

        token.Redeem(Now.AddMinutes(1));

        token.UsedAt.ShouldBe(Now.AddMinutes(1));
        Should.Throw<DomainException>(() => token.Redeem(Now.AddMinutes(2))).Message.ShouldBe(OneTimeToken.InvalidMessage);
    }

    [Fact]
    public void An_expired_token_cannot_be_redeemed()
    {
        var token = OneTimeToken.Issue("user-1", OneTimeTokenPurpose.EmailConfirmation, "hash", Now, Lifetime);

        Should.Throw<DomainException>(() => token.Redeem(Now + Lifetime)).Message.ShouldBe(OneTimeToken.InvalidMessage);
    }

    [Fact]
    public void An_invalidated_token_cannot_be_redeemed()
    {
        var token = OneTimeToken.Issue("user-1", OneTimeTokenPurpose.EmailConfirmation, "hash", Now, Lifetime);

        token.Invalidate(Now);

        token.IsUsable(Now).ShouldBeFalse();
        Should.Throw<DomainException>(() => token.Redeem(Now));
    }
}
