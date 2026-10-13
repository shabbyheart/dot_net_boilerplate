using Boilerplate.Application.Features.Tenants.BootstrapTenant;
using FluentValidation;

namespace Boilerplate.Api.Commands;

/// <summary>
/// <c>bootstrap --tenant-name &lt;name&gt; --admin-user &lt;subject&gt;</c>: creates a tenant and its first administrator
/// without starting the web server (ADR-0022). Run after migrations, as a deployment step.
/// </summary>
internal static partial class BootstrapCommand
{
    public const string Name = "bootstrap";

    public const int Success = 0;
    public const int InvalidArguments = 2;

    private const string Usage = "Usage: bootstrap --tenant-name <name> --admin-user <subject>";

    public static bool IsRequested(string[] args) => args.Length > 0 && args[0] == Name;

    public static async Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(BootstrapCommand));

        var tenantName = OptionValue(args, "--tenant-name");
        var adminUser = OptionValue(args, "--admin-user");
        if (tenantName is null || adminUser is null)
        {
            LogInvalidArguments(logger, Usage);
            return InvalidArguments;
        }

        await using var scope = services.CreateAsyncScope();
        var request = new BootstrapTenantRequest(tenantName, adminUser);

        var validation = await scope.ServiceProvider
            .GetRequiredService<IValidator<BootstrapTenantRequest>>()
            .ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            LogInvalidArguments(logger, validation.ToString(" "));
            return InvalidArguments;
        }

        var result = await scope.ServiceProvider
            .GetRequiredService<BootstrapTenantHandler>()
            .HandleAsync(request, cancellationToken);

        LogCompleted(logger, result.TenantId, result.TenantCreated, result.AdministratorRoleCreated, result.AdministratorAssigned);
        return Success;
    }

    private static string? OptionValue(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Bootstrap not run: {Problem}")]
    private static partial void LogInvalidArguments(ILogger logger, string problem);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Bootstrap completed for tenant {TenantId}: tenant created {TenantCreated}, administrator role created {RoleCreated}, administrator assigned {AdministratorAssigned}")]
    private static partial void LogCompleted(ILogger logger, Guid tenantId, bool tenantCreated, bool roleCreated, bool administratorAssigned);
}
