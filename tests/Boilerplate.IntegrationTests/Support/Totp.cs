using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace Boilerplate.IntegrationTests.Support;

/// <summary>
/// Computes authenticator codes (RFC 6238: HMAC-SHA1, 30-second steps, 6 digits) the way an authenticator app does,
/// from the base32 key the API hands out (ADR-0031).
/// </summary>
internal static class Totp
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>The code for the current time step. The API also accepts adjacent steps, so a step change is harmless.</summary>
    public static string Code(string base32Key) => Code(base32Key, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);

    /// <summary>A valid-looking code that is not the current one.</summary>
    public static string WrongCode(string base32Key)
    {
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var valid = Enumerable.Range(-3, 7).Select(offset => Code(base32Key, step + offset)).ToHashSet();
        return Enumerable.Range(0, 1_000_000)
            .Select(value => value.ToString("D6", CultureInfo.InvariantCulture))
            .First(candidate => !valid.Contains(candidate));
    }

    private static string Code(string base32Key, long step)
    {
        var counter = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        // RFC 6238 and authenticator apps use HMAC-SHA1; its weaknesses do not apply to HMAC.
#pragma warning disable CA5350
        var hash = HMACSHA1.HashData(Decode(base32Key), counter);
#pragma warning restore CA5350

        var offset = hash[^1] & 0x0F;
        var binary = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset)) & 0x7FFFFFFF;
        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] Decode(string base32)
    {
        var bits = 0;
        var value = 0;
        var bytes = new List<byte>();
        foreach (var character in base32.TrimEnd('=').ToUpperInvariant())
        {
            value = (value << 5) | Base32Alphabet.IndexOf(character, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(value >> (bits - 8)));
                bits -= 8;
            }
        }

        return [.. bytes];
    }
}
