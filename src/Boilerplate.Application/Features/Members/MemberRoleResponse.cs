namespace Boilerplate.Application.Features.Members;

/// <summary>A role held by a member.</summary>
public sealed record MemberRoleResponse(Guid Id, string Name, bool IsAdministrator);
