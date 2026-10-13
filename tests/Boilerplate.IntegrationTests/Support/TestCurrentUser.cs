using Boilerplate.Application.Common.Security;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>A current user the test sets explicitly, one per service scope.</summary>
internal sealed class TestCurrentUser : ICurrentUser
{
    public string? UserId { get; set; }
}
