using Boilerplate.Domain.Authorization;
using Boilerplate.Domain.Common;

namespace Boilerplate.UnitTests.Domain;

public sealed class PlatformAdministratorTests
{
    [Fact]
    public void A_platform_administrator_needs_a_bounded_user_id()
    {
        PlatformAdministrator.Create("user-1").UserId.ShouldBe("user-1");
        Should.Throw<DomainException>(() => PlatformAdministrator.Create(" "));
        Should.Throw<DomainException>(() => PlatformAdministrator.Create(new string('u', IAuditable.UserIdMaxLength + 1)));
    }
}
