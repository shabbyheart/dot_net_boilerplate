using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// A Mailpit SMTP server that captures e-mail instead of delivering it, with an HTTP API to inspect what arrived
/// (same image as deploy/compose.yaml).
/// </summary>
public sealed class MailpitFixture : IAsyncLifetime
{
    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;

    private readonly IContainer _container = new ContainerBuilder("axllent/mailpit:v1.31.5")
        .WithPortBinding(SmtpPort, assignRandomHostPort: true)
        .WithPortBinding(HttpPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(HttpPort).ForPath("/readyz")))
        .Build();

    private HttpClient? _api;

    /// <summary>Settings that point the API's SMTP client at this Mailpit instance.</summary>
    public IReadOnlyDictionary<string, string?> SmtpSettings => new Dictionary<string, string?>
    {
        ["Email:Smtp:Host"] = _container.Hostname,
        ["Email:Smtp:Port"] = _container.GetMappedPublicPort(SmtpPort).ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["Email:Smtp:Security"] = "None",
        ["Email:Smtp:FromAddress"] = "noreply@tests.boilerplate.local",
        ["Email:Smtp:FromName"] = "Boilerplate tests",
    };

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        _api = new HttpClient
        {
            BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(HttpPort)}"),
        };
    }

    /// <summary>Waits until an e-mail with <paramref name="subject"/> has arrived and returns it.</summary>
    public async Task<CapturedEmail> WaitForEmailAsync(string subject, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            var list = await _api!.GetFromJsonAsync<MessageList>("/api/v1/messages", cancellationToken);
            var summary = list?.Messages.FirstOrDefault(message => message.Subject == subject);
            if (summary is not null)
            {
                return (await _api!.GetFromJsonAsync<CapturedEmail>($"/api/v1/message/{summary.Id}", cancellationToken))!;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"No e-mail with subject '{subject}' arrived within {timeout}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }
    }

    /// <summary>How many captured e-mails have <paramref name="subject"/>.</summary>
    public async Task<int> CountAsync(string subject, CancellationToken cancellationToken)
    {
        var list = await _api!.GetFromJsonAsync<MessageList>("/api/v1/messages", cancellationToken);
        return list?.Messages.Count(message => message.Subject == subject) ?? 0;
    }

    public async ValueTask DisposeAsync()
    {
        _api?.Dispose();
        await _container.DisposeAsync();
    }

    private sealed record MessageList([property: JsonPropertyName("messages")] IReadOnlyList<MessageSummary> Messages);

    private sealed record MessageSummary(
        [property: JsonPropertyName("ID")] string Id,
        [property: JsonPropertyName("Subject")] string Subject);
}

/// <summary>An e-mail as captured by Mailpit.</summary>
public sealed record CapturedEmail(
    [property: JsonPropertyName("MessageID")] string MessageId,
    [property: JsonPropertyName("Subject")] string Subject,
    [property: JsonPropertyName("Text")] string Text,
    [property: JsonPropertyName("To")] IReadOnlyList<CapturedAddress> To,
    [property: JsonPropertyName("From")] CapturedAddress From);

public sealed record CapturedAddress(
    [property: JsonPropertyName("Address")] string Address,
    [property: JsonPropertyName("Name")] string Name);
