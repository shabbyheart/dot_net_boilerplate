using Boilerplate.Application.Common.Email;
using Boilerplate.Application.Common.Messaging;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Messaging;

/// <summary>E-mail over real SMTP (Mailpit), directly and through the running outbox dispatcher (ADR-0019).</summary>
public sealed class EmailDeliveryTests(ApiFactory factory, MailpitFixture mailpit, PostgresFixture postgres)
    : IClassFixture<ApiFactory>, IClassFixture<MailpitFixture>
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Smtp_sender_delivers_the_message_with_a_stable_message_id()
    {
        await using var services = PersistenceServices.Create(postgres.ConnectionString, settings: mailpit.SmtpSettings);
        await using var scope = services.CreateScope(tenantId: null);
        var subject = $"Direct {Guid.NewGuid():N}";
        var deduplicationId = Guid.NewGuid();

        await scope.ServiceProvider.GetRequiredService<IEmailSender>()
            .SendAsync(new EmailMessage("someone@example.com", subject, "Hello from the tests."), deduplicationId, _cancellationToken);

        var email = await mailpit.WaitForEmailAsync(subject, TimeSpan.FromSeconds(10), _cancellationToken);
        email.To.Select(to => to.Address).ShouldBe(["someone@example.com"]);
        email.From.Address.ShouldBe("noreply@tests.boilerplate.local");
        email.Text.Trim().ShouldBe("Hello from the tests.");
        email.MessageId.ShouldStartWith(deduplicationId.ToString("N"));
    }

    [Fact]
    public async Task Running_api_delivers_an_enqueued_email_and_clears_the_payload()
    {
        await using var api = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration
                .AddInMemoryCollection(mailpit.SmtpSettings)
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Outbox:Enabled"] = "true",
                    ["Outbox:PollingInterval"] = "00:00:00.200",
                })));
        var subject = $"Dispatched {Guid.NewGuid():N}";

        Guid messageId;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<Outbox>()
                .Enqueue(new EmailMessage("recipient@example.com", subject, "Sent by the dispatcher."));
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.SaveChangesAsync(_cancellationToken);
            messageId = (await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Payload.Contains(subject), _cancellationToken)).Id;
        }

        var email = await mailpit.WaitForEmailAsync(subject, TimeSpan.FromSeconds(30), _cancellationToken);
        email.MessageId.ShouldStartWith(messageId.ToString("N"));

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var row = await WaitForProcessedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), messageId);
            row.Payload.ShouldBeEmpty();
        }

        (await mailpit.CountAsync(subject, _cancellationToken)).ShouldBe(1);
    }

    // The e-mail can arrive a moment before the dispatcher records success.
    private async Task<OutboxMessage> WaitForProcessedAsync(AppDbContext db, Guid messageId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var row = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == messageId, _cancellationToken);
            if (row.ProcessedAt is not null)
            {
                return row;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), _cancellationToken);
        }

        throw new TimeoutException($"Outbox message {messageId} was not marked as processed.");
    }
}
