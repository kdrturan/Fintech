namespace Banking.Modules.Identity.Infrastructure.Tokens;

/// <summary>
/// How access tokens are issued. Bound from the same <c>Jwt</c> section that <c>Banking.Web</c> reads to validate
/// them, so issuer, audience and key cannot drift apart.
/// </summary>
public sealed class AccessTokenOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>The RSA private key, base64 of its PKCS#8 encoding. Never in the repository.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Short on purpose: a stolen token cannot be revoked, it can only expire.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
}
