using Microsoft.AspNetCore.Identity;

namespace Boilerplate.Infrastructure.Accounts;

/// <summary>
/// A local account (ADR-0025). The e-mail address is also the user name. Its id is the token subject (<c>sub</c>),
/// which role assignments refer to (ADR-0017).
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }
}
