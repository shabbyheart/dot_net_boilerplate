namespace Boilerplate.Api.Controllers;

/// <summary>The caller's identity as seen by the API.</summary>
/// <param name="UserId">The token's <c>sub</c> claim.</param>
/// <param name="TenantId">The token's <c>tenant_id</c> claim, or <see langword="null"/>.</param>
public sealed record MeResponse(string UserId, Guid? TenantId);
