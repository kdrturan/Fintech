using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Banking.Modules.Identity.Contracts;
using Banking.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.IntegrationTests;

/// <summary>Who may do what: customers open accounts, tellers pay cash in, and nobody becomes a teller by asking.</summary>
public sealed class RolesTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private readonly BankingClient _client = api.CreateBankingClient();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_customer_cannot_pay_money_into_their_own_account()
    {
        var account = await _client.OpenAccountAsync();

        using var response = await DepositAsync(_client.Http, account.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await _client.BalanceAsync(account.Id)).ShouldBe(0m);
    }

    [Fact]
    public async Task Paying_in_without_a_token_is_unauthorized_not_forbidden()
    {
        var account = await _client.OpenAccountAsync();
        using var anonymous = api.CreateAnonymousClient();

        using var response = await DepositAsync(anonymous, account.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_teller_pays_into_a_customers_account()
    {
        var account = await _client.OpenAccountAsync();

        using var response = await DepositAsync(_client.Teller, account.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.BalanceAsync(account.Id)).ShouldBe(10m);
    }

    [Fact]
    public async Task A_forbidden_request_does_not_use_up_its_idempotency_key()
    {
        var account = await _client.OpenAccountAsync();
        var key = BankingClient.NewKey();

        using var forbidden = await DepositAsync(_client.Http, account.Id, key);
        using var allowed = await DepositAsync(_client.Teller, account.Id, key);

        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_teller_cannot_open_an_account()
    {
        using var response = await _client.Teller.PostAsJsonAsync("/accounts", new { currency = "TRY" }, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Asking_for_a_role_at_registration_does_not_grant_it()
    {
        using var anonymous = api.CreateAnonymousClient();
        var email = BankingApiFactory.NewEmail();
        using var registered = await anonymous.PostAsJsonAsync(
            "/auth/register",
            new { email, password = BankingApiFactory.Password, role = "Teller", roles = TellerRole },
            Cancellation);
        registered.StatusCode.ShouldBe(HttpStatusCode.Created);

        (await RolesOfAsync(email)).ShouldBe(["Customer"]);
    }

    [Fact]
    public async Task The_configured_teller_has_the_teller_role_and_no_other()
    {
        var me = (await _client.Teller.GetFromJsonAsync<MeDto>("/me", Cancellation))!;

        me.Email.ShouldBe(BankingApiFactory.TellerEmail);
        me.Roles.ShouldBe(["Teller"]);
    }

    [Fact]
    public async Task Starting_again_with_the_same_teller_changes_nothing()
    {
        await using var restarted = api.WithWebHostBuilder(_ => { });
        using var client = restarted.CreateClient();

        (await RolesOfAsync(BankingApiFactory.TellerEmail)).ShouldBe(["Teller"]);
    }

    [Fact]
    public async Task A_customer_who_holds_the_configured_address_is_not_promoted()
    {
        var email = BankingApiFactory.NewEmail();
        await api.RegisterAndSignInAsync(email);

        await using var restarted = api.WithWebHostBuilder(builder => builder.UseSetting("SeedTeller:Email", email));
        using var client = restarted.CreateClient();

        (await RolesOfAsync(email)).ShouldBe(["Customer"]);
    }

    [Fact]
    public async Task Losing_the_role_counts_from_the_next_token()
    {
        var email = BankingApiFactory.NewEmail();
        await api.RegisterAndSignInAsync(email);
        await WithUserAsync(email, (users, user) => users.AddToRoleAsync(user, IdentityRoles.Teller));
        var (_, token) = await SignInAsync(email);
        var account = await _client.OpenAccountAsync();

        await WithUserAsync(email, (users, user) => users.RemoveFromRoleAsync(user, IdentityRoles.Teller));

        // Known limitation: the role is in the token, so the old one keeps working until it expires.
        using var before = await DepositAsync(token, account.Id);
        before.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (_, next) = await SignInAsync(email);
        using var after = await DepositAsync(next, account.Id);
        after.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static readonly string[] TellerRole = ["Teller"];

    private static async Task<HttpResponseMessage> DepositAsync(HttpClient http, Guid accountId, string? key = null)
    {
        using var request = NewDeposit(accountId, key);
        return await http.SendAsync(request, Cancellation);
    }

    private async Task<HttpResponseMessage> DepositAsync(string accessToken, Guid accountId)
    {
        using var anonymous = api.CreateAnonymousClient();
        using var request = NewDeposit(accountId, key: null);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await anonymous.SendAsync(request, Cancellation);
    }

    private static HttpRequestMessage NewDeposit(Guid accountId, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/accounts/{accountId}/deposits")
        {
            Content = JsonContent.Create(new { amount = 10m, currency = "TRY" }),
        };
        request.Headers.Add("Idempotency-Key", key ?? BankingClient.NewKey());
        return request;
    }

    private async Task<(HttpStatusCode Status, string AccessToken)> SignInAsync(string email)
    {
        using var anonymous = api.CreateAnonymousClient();
        using var response = await anonymous.PostAsJsonAsync("/auth/login", new { email, password = BankingApiFactory.Password }, Cancellation);
        return (response.StatusCode, (await response.Content.ReadFromJsonAsync<LoginDto>(Cancellation))!.AccessToken);
    }

    private async Task<IList<string>> RolesOfAsync(string email)
    {
        IList<string> roles = [];
        await WithUserAsync(email, async (users, user) =>
        {
            roles = await users.GetRolesAsync(user);
            return IdentityResult.Success;
        });
        return roles;
    }

    private async Task WithUserAsync(string email, Func<UserManager<User>, User, Task<IdentityResult>> action)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = (await users.FindByEmailAsync(email)).ShouldNotBeNull();
        (await action(users, user)).Succeeded.ShouldBeTrue();
    }
}
