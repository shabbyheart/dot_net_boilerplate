using Boilerplate.Application.Common.Pagination;

namespace Boilerplate.Application.Features.Tenants.ListTenants;

/// <summary>Lists every tenant, ordered by name.</summary>
public sealed record ListTenantsRequest : PageRequest;
