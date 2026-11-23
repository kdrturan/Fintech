using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Banking.Modules.Identity.Contracts;
using Banking.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Banking.IntegrationTests;

/// <summary>Staying signed in: rotation, what happens when a token is used twice, and signing out.</summary>
public sealed class RefreshTokenTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private readonly HttpClient _anonymous = api.CreateAnonymousClient();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Signing_in_returns_a_refresh_token_for_14_days_that_no_cache_may_keep()
    {
        var email = await RegisterAsync();

        using var response = await _anonymous.PostAsJsonAsync("/auth/login", new { email, password = BankingApiFactory.Password }, Cancellation);

        var login = (await response.Content.ReadFromJsonAsync<LoginDto>(Cancellation))!;
        login.RefreshToken.Length.ShouldBe(43);
        (login.RefreshTokenExpiresAt - DateTimeOffset.UtcNow).ShouldBeInRange(TimeSpan.FromDays(14) - TimeSpan.FromMinutes(1), TimeSpan.FromDays(14));
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task Refreshing_returns_a_new_pair_that_works()
    {
        var first = await SignInAsync(await RegisterAsync());

        using var response = await RefreshAsync(first.RefreshToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        var second = (await response.Content.ReadFromJsonAsync<LoginDto>(Cancellation))!;
        second.RefreshToken.ShouldNotBe(first.RefreshToken);
        (await MeStatusAsync(second.AccessToken)).ShouldBe(HttpStatusCode.OK);
        new JsonWebToken(second.AccessToken).Subject.ShouldBe(new JsonWebToken(first.AccessToken).Subject);
        (await RefreshStatusAsync(second.RefreshToken)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Using_a_token_twice_ends_the_session_including_its_newest_token()
    {
        var first = await SignInAsync(await RegisterAsync());
        var second = await RefreshedAsync(first.RefreshToken);

        using var reuse = await RefreshAsync(first.RefreshToken);

        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await BankingClient.ErrorCodeAsync(reuse)).ShouldBe("auth.invalid_refresh_token");
        (await RefreshStatusAsync(second.RefreshToken)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_reuse_ends_only_its_own_session()
    {
        var email = await RegisterAsync();
        var phone = await SignInAsync(email);
        var tablet = await SignInAsync(email);
        await RefreshedAsync(phone.RefreshToken);

        (await RefreshStatusAsync(phone.RefreshToken)).ShouldBe(HttpStatusCode.Unauthorized);

        (await RefreshStatusAsync(tablet.RefreshToken)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unknown_token_is_rejected()
    {
        using var response = await RefreshAsync("no-such-token");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("auth.invalid_refresh_token");
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        var login = await SignInAsync(await RegisterAsync());
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
            var hash = HashOf(login.RefreshToken);
            (await db.Database.ExecuteSqlAsync($"UPDATE identity.refresh_tokens SET expires_at = now() - interval '1 second' WHERE token_hash = {hash}", Cancellation)).ShouldBe(1);
        }

        (await RefreshStatusAsync(login.RefreshToken)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Of_concurrent_refreshes_with_one_token_exactly_one_succeeds()
    {
        const int Requests = 10;
        var login = await SignInAsync(await RegisterAsync());
        var hash = HashOf(login.RefreshToken);

        // Left to chance the requests rarely overlap, and the test passes without any lock in the code. So hold the
        // session's row here until every request is waiting in the database, then let them all go at once.
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(Cancellation);
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM identity.sessions WHERE id = (SELECT session_id FROM identity.refresh_tokens WHERE token_hash = {hash}) FOR UPDATE",
            Cancellation);

        var requests = Enumerable.Range(0, Requests).Select(_ => RefreshStatusAsync(login.RefreshToken)).ToList();
        await WaitUntilBlockedAsync(Requests);
        await transaction.RollbackAsync(Cancellation);
        var statuses = await Task.WhenAll(requests);

        statuses.Count(status => status == HttpStatusCode.OK).ShouldBe(1);
        statuses.Count(status => status == HttpStatusCode.Unauthorized).ShouldBe(Requests - 1);
    }

    [Fact]
    public async Task The_database_holds_the_hash_and_not_the_token()
    {
        var login = await SignInAsync(await RegisterAsync());

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var hashes = await db.RefreshTokens.AsNoTracking().Select(token => token.TokenHash).ToListAsync(Cancellation);

        hashes.ShouldContain(HashOf(login.RefreshToken));
        hashes.ShouldNotContain(login.RefreshToken);
    }

    [Fact]
    public async Task A_role_given_after_sign_in_is_in_the_refreshed_token()
    {
        var email = await RegisterAsync();
        var login = await SignInAsync(email);
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = (await users.FindByEmailAsync(email)).ShouldNotBeNull();
            (await users.AddToRoleAsync(user, IdentityRoles.Teller)).Succeeded.ShouldBeTrue();
        }

        var refreshed = await RefreshedAsync(login.RefreshToken);

        Roles(login.AccessToken).ShouldBe(["Customer"]);
        Roles(refreshed.AccessToken).ShouldBe(["Customer", "Teller"], ignoreOrder: true);
    }

    [Fact]
    public async Task Signing_out_kills_the_refresh_token_but_the_access_token_lives_until_it_expires()
    {
        var login = await SignInAsync(await RegisterAsync());

        (await LogoutStatusAsync(login.AccessToken, login.RefreshToken)).ShouldBe(HttpStatusCode.NoContent);

        (await RefreshStatusAsync(login.RefreshToken)).ShouldBe(HttpStatusCode.Unauthorized);
        (await MeStatusAsync(login.AccessToken)).ShouldBe(HttpStatusCode.OK);
        (await LogoutStatusAsync(login.AccessToken, login.RefreshToken)).ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Signing_out_needs_an_access_token()
    {
        var login = await SignInAsync(await RegisterAsync());

        using var response = await _anonymous.PostAsJsonAsync("/auth/logout", new { refreshToken = login.RefreshToken }, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await RefreshStatusAsync(login.RefreshToken)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Nobody_can_end_someone_elses_session()
    {
        var ahmet = await SignInAsync(await RegisterAsync());
        var ayse = await SignInAsync(await RegisterAsync());

        (await LogoutStatusAsync(ayse.AccessToken, ahmet.RefreshToken)).ShouldBe(HttpStatusCode.NoContent);

        (await RefreshStatusAsync(ahmet.RefreshToken)).ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Waits until this many connections to the test's database are waiting for a lock.</summary>
    private async Task WaitUntilBlockedAsync(int expected)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var scope = api.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
            var waiting = await db.Database
                .SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'""")
                .SingleAsync(Cancellation);
            if (waiting == expected)
            {
                return;
            }

            await Task.Delay(50, Cancellation);
        }

        Assert.Fail($"{expected} requests never waited for the lock together.");
    }

    private async Task<string> RegisterAsync()
    {
        var email = BankingApiFactory.NewEmail();
        using var response = await _anonymous.PostAsJsonAsync("/auth/register", new { email, password = BankingApiFactory.Password }, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return email;
    }

    private async Task<LoginDto> SignInAsync(string email)
    {
        using var response = await _anonymous.PostAsJsonAsync("/auth/login", new { email, password = BankingApiFactory.Password }, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<LoginDto>(Cancellation))!;
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _anonymous.PostAsJsonAsync("/auth/refresh", new { refreshToken }, Cancellation);

    private async Task<LoginDto> RefreshedAsync(string refreshToken)
    {
        using var response = await RefreshAsync(refreshToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<LoginDto>(Cancellation))!;
    }

    private async Task<HttpStatusCode> RefreshStatusAsync(string refreshToken)
    {
        using var response = await RefreshAsync(refreshToken);
        return response.StatusCode;
    }

    private async Task<HttpStatusCode> MeStatusAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _anonymous.SendAsync(request, Cancellation);
        return response.StatusCode;
    }

    private async Task<HttpStatusCode> LogoutStatusAsync(string accessToken, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout") { Content = JsonContent.Create(new { refreshToken }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _anonymous.SendAsync(request, Cancellation);
        return response.StatusCode;
    }

    private static string HashOf(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static List<string> Roles(string accessToken) =>
        new JsonWebToken(accessToken).Claims.Where(claim => claim.Type == "role").Select(claim => claim.Value).ToList();
}
