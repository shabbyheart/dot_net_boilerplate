using Boilerplate.Application.Common.Auditing;
using Boilerplate.Application.Features.Auditing.ListAuditLog;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Application.Features.Auditing;

/// <summary>Registers the audit log explicitly (ADR-0004, ADR-0028).</summary>
public static class AuditingFeature
{
    public static IServiceCollection AddAuditingFeature(this IServiceCollection services)
    {
        services.AddScoped<AuditLog>();

        services.AddScoped<ListAuditLogHandler>();
        services.AddScoped<IValidator<ListAuditLogRequest>, ListAuditLogValidator>();

        return services;
    }
}
