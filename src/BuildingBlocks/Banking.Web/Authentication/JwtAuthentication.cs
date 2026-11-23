using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Banking.Web.Authentication;

public static class JwtAuthentication
{
    /// <summary>The claims this API reads. Kept as the short JWT names; see <c>MapInboundClaims</c> below.</summary>
    public const string UserIdClaim = "sub";

    public const string RoleClaim = "role";

    /// <summary>
    /// Accepts access tokens issued by the Identity module, and makes every endpoint require one unless it is marked
    /// <c>[AllowAnonymous]</c>. Settings are checked when the application starts, not here, so tools that build the
    /// host without running it (<c>dotnet ef</c>) work without a key; the application itself refuses to start without one.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Issuer) && !string.IsNullOrWhiteSpace(settings.Audience), "Jwt:Issuer and Jwt:Audience must be configured.")
            .Validate(settings => settings.HasUsableSigningKey(), "Jwt:SigningKey must be a base64 PKCS#8 RSA private key of at least 2048 bits. See the README.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, jwt) =>
            {
                // Keep "sub" and "role" as they are in the token instead of renaming them to long XML claim URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = jwt.Value.CreateValidationKey(),

                    // Only the algorithm we sign with. Without this, a token signed by the same key with another RSA
                    // algorithm, such as PS256, would be accepted too.
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],

                    NameClaimType = UserIdClaim,
                    RoleClaimType = RoleClaim,

                    // The default tolerance for clock differences is 5 minutes, which would let a 15-minute token
                    // live for 20. The servers share one clock source, so 30 seconds is enough.
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        // Secure by default: an endpoint someone forgets to protect is still protected.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
