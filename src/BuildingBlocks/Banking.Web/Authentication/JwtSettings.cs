using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Banking.Web.Authentication;

/// <summary>
/// What every API needs to check an access token: who issued it, who it is for, and the key that signed it.
/// Bound from the <c>Jwt</c> configuration section. The Identity module reads the same section to issue tokens.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// The RSA private key, as base64 of its PKCS#8 encoding. It never goes into the repository: user-secrets in
    /// development, a secret store in production. Only the public half is used here, to verify signatures; a
    /// service that only validates tokens (phase 6) will be given the public key alone.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Reads the public key out of <see cref="SigningKey"/>. Throws if it is not a usable RSA key.</summary>
    public RsaSecurityKey CreateValidationKey()
    {
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(SigningKey), out _);
        return new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: false));
    }

    internal bool HasUsableSigningKey()
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(SigningKey), out _);
            return rsa.KeySize >= 2048;
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return false;
        }
    }
}
