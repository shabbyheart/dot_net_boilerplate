using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Boilerplate.Infrastructure.Accounts;

/// <summary>
/// How local sign-in signs access tokens (<c>Accounts:Tokens</c> section; ADR-0025). Required only when accounts are
/// enabled. The signing keys are secrets: user secrets in development, environment variables or a vault elsewhere.
/// </summary>
public sealed class AccessTokenOptions
{
    public const string SectionName = "Accounts:Tokens";

    /// <summary>HMAC-SHA256 needs at least 256 bits of key.</summary>
    public const int MinimumKeyBytes = 32;

    public string? Issuer { get; set; }

    public string? Audience { get; set; }

    /// <summary>Base64 key that signs new tokens.</summary>
    public string? SigningKey { get; set; }

    /// <summary>Base64 keys that still validate tokens signed before a key rotation. Remove them after one token lifetime.</summary>
    public IList<string> PreviousSigningKeys { get; } = [];

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Signs new tokens with <see cref="SigningKey"/>.</summary>
    public SigningCredentials CreateSigningCredentials() =>
        new(CreateKey(SigningKey!), SecurityAlgorithms.HmacSha256);

    /// <summary>The current and previous keys, for validating tokens this API issued.</summary>
    public IReadOnlyList<SecurityKey> CreateValidationKeys() =>
        [CreateKey(SigningKey!), .. PreviousSigningKeys.Select(CreateKey)];

    internal static bool IsValidKey(string? value)
    {
        var bytes = new byte[value?.Length ?? 0];
        return Convert.TryFromBase64String(value ?? string.Empty, bytes, out var written) && written >= MinimumKeyBytes;
    }

    // The key id is derived from the key, so a token names the key that signed it without revealing it.
    private static SymmetricSecurityKey CreateKey(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        return new SymmetricSecurityKey(bytes) { KeyId = Convert.ToHexStringLower(SHA256.HashData(bytes))[..16] };
    }
}
