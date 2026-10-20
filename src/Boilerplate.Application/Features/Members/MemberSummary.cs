namespace Boilerplate.Application.Features.Members;

/// <summary>A member before their roles are read.</summary>
internal sealed record MemberSummary(string UserId, DateTimeOffset JoinedAt);
