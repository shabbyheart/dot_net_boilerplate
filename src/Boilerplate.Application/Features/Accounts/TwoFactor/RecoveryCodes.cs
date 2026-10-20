using System.Security.Cryptography;
using Boilerplate.Application.Common.Security;

namespace Boilerplate.Application.Features.Accounts.TwoFactor;

/// <summary>
/// Single-use codes that replace the authenticator once each (ADR-0031). 80 random bits, shown as four groups of four
/// base32 characters. Only hashes of the normalized form are stored.
/// </summary>
public static class RecoveryCodes
{
    public const int Count = 10;

    /// <summary>Upper bound for a recovery code sent back by a client; generated codes are 19 characters.</summary>
    public const int MaxLength = 32;

    // RFC 4648 base32: no 0, 1, 8 or 9, so codes are easy to read and type.
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int Length = 16;

    public static IReadOnlyList<string> Create() =>
        Enumerable.Range(0, Count).Select(_ => CreateOne()).ToList();

    /// <summary>The stored form: separators and spaces removed, upper case, hashed.</summary>
    public static string Hash(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var normalized = string.Concat(code.Where(character => character is not ('-' or ' '))).ToUpperInvariant();
        return SecureToken.Hash(normalized);
    }

    private static string CreateOne()
    {
        var characters = RandomNumberGenerator.GetString(Alphabet, Length);
        return string.Join('-', characters.Chunk(4).Select(group => new string(group)));
    }
}
