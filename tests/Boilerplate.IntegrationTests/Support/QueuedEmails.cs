using System.Text.Json;
using System.Text.RegularExpressions;
using Boilerplate.Application.Common.Email;
using Boilerplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// E-mails read from the outbox table. The dispatcher is off in tests, so queued messages keep their payload.
/// </summary>
internal static partial class QueuedEmails
{
    /// <summary>The e-mails queued for <paramref name="to"/>, oldest first.</summary>
    public static async Task<List<EmailMessage>> ToAsync(IServiceProvider services, string to, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var payloads = await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages
            .AsNoTracking()
            .Where(message => message.Type == EmailMessage.OutboxType && message.Payload.Contains(to))
            .OrderBy(message => message.Id)
            .Select(message => message.Payload)
            .ToListAsync(cancellationToken);

        return payloads
            .Select(payload => JsonSerializer.Deserialize<EmailMessage>(payload, JsonSerializerOptions.Web)!)
            .Where(email => email.To == to)
            .ToList();
    }

    /// <summary>The token from the link in an e-mail.</summary>
    public static string TokenFrom(EmailMessage email)
    {
        var match = TokenParameter().Match(email.TextBody);
        match.Success.ShouldBeTrue($"No link with a token in: {email.TextBody}");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    [GeneratedRegex(@"[?&]token=([^\s&]+)")]
    private static partial Regex TokenParameter();
}
