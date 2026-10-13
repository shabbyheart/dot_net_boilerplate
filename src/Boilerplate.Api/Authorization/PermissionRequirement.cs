using Microsoft.AspNetCore.Authorization;

namespace Boilerplate.Api.Authorization;

/// <summary>The caller must hold <see cref="Permission"/> in their current tenant (ADR-0017).</summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
