using System.Globalization;
using Boilerplate.Application.Common.Email;

namespace Boilerplate.Application.Features.Invitations;

/// <summary>The invitation e-mail. Its body contains a single-use link: never log it (ADR-0019).</summary>
internal static class InvitationEmails
{
    public static EmailMessage Invite(string to, string tenantName, string roleName, string pageUrl, string token, TimeSpan lifetime)
    {
        var separator = pageUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var link = $"{pageUrl}{separator}token={Uri.EscapeDataString(token)}";
        var days = string.Create(CultureInfo.InvariantCulture, $"{lifetime.TotalDays:0.#}");

        return new EmailMessage(
            to,
            $"You are invited to {tenantName}",
            $"""
            You have been invited to join {tenantName} with the role {roleName}.

            Sign in, then open this link to accept:

            {link}

            The link can be used once and expires in {days} day(s). Anyone with the link can accept it, so do not forward it.
            If you did not expect this invitation, ignore this e-mail.
            """);
    }
}
