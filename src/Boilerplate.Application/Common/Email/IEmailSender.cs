namespace Boilerplate.Application.Common.Email;

/// <summary>Sends e-mail through an external provider (ADR-0019). Implemented in Infrastructure.</summary>
public interface IEmailSender
{
    /// <param name="message">The e-mail.</param>
    /// <param name="deduplicationId">Identical on every attempt for the same e-mail, so receivers can recognise duplicates.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    Task SendAsync(EmailMessage message, Guid deduplicationId, CancellationToken cancellationToken);
}
