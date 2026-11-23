using System.Security.Cryptography;
using Banking.Modules.Identity.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Banking.Modules.Identity.Infrastructure.Tokens;

/// <summary>
/// Issues RS256-signed JWTs. Signing needs the private key; checking a signature needs only the public half.
/// The token carries the user id and roles only: its payload is readable by anyone who holds it.
/// </summary>
internal sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    // The claim name Banking.Web validates roles with (JwtAuthentication.RoleClaim).
    private const string RoleClaim = "role";

    private readonly AccessTokenOptions _options;
    private readonly TimeProvider _time;
    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtAccessTokenIssuer(IOptions<AccessTokenOptions> options, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _time = time;

        // The key is handed over as parameters, not as an RSA object: this issuer is a singleton used by many requests
        // at once, and RSA objects are not documented as thread-safe. Parameters are plain data, safe to share.
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(_options.SigningKey), out _);
        _credentials = new SigningCredentials(new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: true)), SecurityAlgorithms.RsaSha256);
    }

    public AccessToken Issue(Guid userId, IReadOnlyList<string> roles)
    {
        // JWT times are whole seconds; round down so ExpiresAt matches the token's "exp" exactly.
        var now = DateTimeOffset.FromUnixTimeSeconds(_time.GetUtcNow().ToUnixTimeSeconds());
        var expiresAt = now + _options.AccessTokenLifetime;

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
                [RoleClaim] = roles.ToArray(),
            },
        });

        return new AccessToken(token, expiresAt);
    }
}
