using System.Globalization;
using Boilerplate.Application.Common.Email;

namespace Boilerplate.Application.Features.Accounts;

/// <summary>The e-mails the account module sends. Bodies contain single-use links: never log them (ADR-0019).</summary>
internal static class AccountEmails
{
    public static EmailMessage ConfirmEmail(string to, string pageUrl, string token, TimeSpan lifetime) => new(
        to,
        "Confirm your e-mail address",
        $"""
        Confirm your e-mail address to finish creating your account:

        {Link(pageUrl, token)}

        The link can be used once and expires in {Describe(lifetime)}. If you did not create an account, ignore this e-mail.
        """);

    public static EmailMessage AlreadyRegistered(string to) => new(
        to,
        "You already have an account",
        """
        Someone tried to create an account with this e-mail address, which already has one.

        If it was you, sign in, or reset your password if you have forgotten it. Otherwise, ignore this e-mail.
        """);

    public static EmailMessage ResetPassword(string to, string pageUrl, string token, TimeSpan lifetime) => new(
        to,
        "Reset your password",
        $"""
        Use this link to choose a new password:

        {Link(pageUrl, token)}

        The link can be used once and expires in {Describe(lifetime)}. If you did not ask for this, ignore this e-mail;
        your password stays the same.
        """);

    private static string Link(string pageUrl, string token)
    {
        var separator = pageUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{pageUrl}{separator}token={Uri.EscapeDataString(token)}";
    }

    private static string Describe(TimeSpan lifetime) => lifetime.TotalHours >= 1
        ? string.Create(CultureInfo.InvariantCulture, $"{lifetime.TotalHours:0.#} hour(s)")
        : string.Create(CultureInfo.InvariantCulture, $"{lifetime.TotalMinutes:0} minute(s)");
}
