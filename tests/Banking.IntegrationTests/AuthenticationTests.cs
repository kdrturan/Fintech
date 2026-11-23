using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Banking.IntegrationTests;

/// <summary>
/// What the API accepts as proof of identity. Most tests build their own token with the test signing key, change one
/// thing about it, and expect a 401; the first one proves such a token is accepted when nothing is wrong with it.
/// </summary>
public sealed class AuthenticationTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private static readonly string[] CustomerRole = ["Customer"];

    private readonly HttpClient _anonymous = api.CreateAnonymousClient();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_token_with_the_right_key_audience_and_lifetime_is_accepted()
    {
        using var response = await GetMeAsync(Token(api.CustomerId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_request_without_a_token_is_rejected()
    {
        using var response = await _anonymous.GetAsync($"/accounts/{Guid.NewGuid()}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_health_check_needs_no_token()
    {
        using var response = await _anonymous.GetAsync("/health", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_access_token_carries_the_user_id_and_role_and_nothing_personal()
    {
        var email = BankingApiFactory.NewEmail();
        var (userId, accessToken) = await api.RegisterAndSignInAsync(email);

        var token = new JsonWebToken(accessToken);

        token.Alg.ShouldBe(SecurityAlgorithms.RsaSha256);
        token.Subject.ShouldBe(userId.ToString());
        token.GetClaim("role").Value.ShouldBe("Customer");
        token.Issuer.ShouldBe("banking-api");
        token.Audiences.ShouldBe(["banking-api"]);
        (token.ValidTo - token.IssuedAt).ShouldBe(TimeSpan.FromMinutes(15));
        token.Claims.Select(claim => claim.Value).ShouldNotContain(email);
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_rejected()
    {
        using var otherKey = RSA.Create(2048);

        using var response = await GetMeAsync(Token(api.CustomerId, key: otherKey.ExportParameters(includePrivateParameters: true)));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_signed_with_another_algorithm_is_rejected()
    {
        // The right key, but PS256 instead of RS256: only ValidAlgorithms stands in the way.
        using var response = await GetMeAsync(Token(api.CustomerId, algorithm: SecurityAlgorithms.RsaSsaPssSha256));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_that_expired_a_minute_ago_is_rejected()
    {
        using var response = await GetMeAsync(Token(api.CustomerId, expires: DateTime.UtcNow.AddMinutes(-1)));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_for_another_audience_is_rejected()
    {
        using var response = await GetMeAsync(Token(api.CustomerId, audience: "another-api"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_whose_payload_was_changed_is_rejected()
    {
        var parts = Token(api.CustomerId).Split('.');
        var payload = JsonNode.Parse(Base64UrlEncoder.Decode(parts[1]))!;
        payload["role"] = "Teller";
        var tampered = $"{parts[0]}.{Base64UrlEncoder.Encode(payload.ToJsonString())}.{parts[2]}";

        using var response = await GetMeAsync(tampered);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_for_a_user_who_does_not_exist_is_signed_but_finds_no_one()
    {
        using var response = await GetMeAsync(Token(Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("auth.user_not_found");
    }

    private async Task<HttpResponseMessage> GetMeAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _anonymous.SendAsync(request, Cancellation);
    }

    /// <summary>A token like the API's own, unless a parameter says otherwise.</summary>
    private static string Token(
        Guid subject,
        RSAParameters? key = null,
        string audience = "banking-api",
        DateTime? expires = null,
        string algorithm = SecurityAlgorithms.RsaSha256)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(15);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "banking-api",
            Audience = audience,
            IssuedAt = expiresAt.AddMinutes(-15),
            NotBefore = expiresAt.AddMinutes(-15),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(key ?? BankingApiFactory.SigningKey), algorithm),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = subject.ToString(),
                ["role"] = CustomerRole,
            },
        });
    }
}
