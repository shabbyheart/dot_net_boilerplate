using Boilerplate.Application.Common.Pagination;

namespace Boilerplate.Application.Features.Invitations.ListInvitations;

/// <summary>Lists the current tenant's invitations, newest first.</summary>
public sealed record ListInvitationsRequest : PageRequest;
