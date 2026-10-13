namespace Boilerplate.Application.Common.Security;

/// <summary>
/// The user the current operation acts for (ADR-0016): the token's <c>sub</c> claim.
/// <see langword="null"/> when there is no authenticated user, for example in background work.
/// </summary>
public interface ICurrentUser
{
    string? UserId { get; }
}
