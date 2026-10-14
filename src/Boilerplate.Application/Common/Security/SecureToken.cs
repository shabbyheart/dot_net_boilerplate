using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Boilerplate.Application.Common.Security;

/// <summary>Random tokens handed to clients, and the hashes stored instead of them (ADR-0025, ADR-0027).</summary>
internal static class SecureToken
{
    /// <summary>Upper bound for a token sent back by a client; generated tokens are 43 characters.</summary>
    public const int MaxLength = 128;

    /// <summary>256 random bits, URL-safe.</summary>
    public static string Create() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// SHA-256 is enough here: the tokens are random and long, so they cannot be guessed from the hash, unlike passwords.
    /// </summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
