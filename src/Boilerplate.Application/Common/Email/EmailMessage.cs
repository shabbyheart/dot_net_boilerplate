namespace Boilerplate.Application.Common.Email;

/// <summary>
/// An e-mail to send. Features never send e-mail directly: they call <c>outbox.Enqueue(new EmailMessage(...))</c>
/// and save, so the e-mail is sent if and only if their change commits (ADR-0019).
/// </summary>
/// <param name="To">Recipient address.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="TextBody">Plain-text body. Stored in the outbox until delivered, then cleared.</param>
public sealed record EmailMessage(string To, string Subject, string TextBody)
{
    /// <summary>The stable outbox type name. Never change it once messages exist.</summary>
    public const string OutboxType = "email";
}
