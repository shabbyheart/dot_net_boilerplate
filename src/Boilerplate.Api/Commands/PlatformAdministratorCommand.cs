using Boilerplate.Application.Features.PlatformAdministrators;
using Boilerplate.Domain.Common;

namespace Boilerplate.Api.Commands;

/// <summary>
/// <c>platform-admin grant|revoke --user &lt;subject&gt;</c>: grants or revokes platform administration without starting
/// the web server (ADR-0027). The only way to create a platform administrator.
/// </summary>
internal static partial class PlatformAdministratorCommand
{
    public const string Name = "platform-admin";

    private const string Usage = "Usage: platform-admin grant|revoke --user <subject>";

    public static bool IsRequested(string[] args) => args.Length > 0 && args[0] == Name;

    public static async Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PlatformAdministratorCommand));

        var action = args.Length > 1 ? args[1] : null;
        var userId = OptionValue(args, "--user");
        if (action is not ("grant" or "revoke") || userId is null)
        {
            LogInvalidArguments(logger, Usage);
            return BootstrapCommand.InvalidArguments;
        }

        await using var scope = services.CreateAsyncScope();
        var grants = scope.ServiceProvider.GetRequiredService<PlatformAdministratorGrants>();

        try
        {
            var changed = action == "grant"
                ? await grants.GrantAsync(userId, cancellationToken)
                : await grants.RevokeAsync(userId, cancellationToken);
            LogCompleted(logger, action, userId, changed);
            return BootstrapCommand.Success;
        }
        catch (DomainException exception)
        {
            LogInvalidArguments(logger, exception.Message);
            return BootstrapCommand.InvalidArguments;
        }
    }

    private static string? OptionValue(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Platform administrator command not run: {Problem}")]
    private static partial void LogInvalidArguments(ILogger logger, string problem);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Platform administrator {Action} completed for user {UserId}: changed {Changed}")]
    private static partial void LogCompleted(ILogger logger, string action, string userId, bool changed);
}
