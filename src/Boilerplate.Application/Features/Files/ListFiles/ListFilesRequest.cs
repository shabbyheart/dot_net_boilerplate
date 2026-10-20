using Boilerplate.Application.Common.Pagination;

namespace Boilerplate.Application.Features.Files.ListFiles;

/// <summary>Lists the current tenant's files, newest first.</summary>
public sealed record ListFilesRequest : PageRequest;
