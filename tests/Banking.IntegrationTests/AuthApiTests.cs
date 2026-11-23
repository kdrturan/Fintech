using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Banking.IntegrationTests;

/// <summary>Registration and sign-in over HTTP, as a mobile app would use them.</summary>
public sealed class AuthApiTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private const string Password = BankingApiFactory.Password;

    private readonly HttpClient _anonymous = api.CreateAnonymousClient();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Registering_creates_a_customer_who_can_sign_in_and_see_themselves()
    {
        var email = BankingApiFactory.NewEmail();

        using var registered = await RegisterAsync(email, Password);
        registered.StatusCode.ShouldBe(HttpStatusCode.Created);
        var user = (await registered.Content.ReadFromJsonAsync<RegisteredUserDto>(Cancellation))!;

        using var signedIn = await LoginAsync(email, Password);
        signedIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        var login = (await signedIn.Content.ReadFromJsonAsync<LoginDto>(Cancellation))!;
        login.TokenType.ShouldBe("Bearer");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        using var response = await _anonymous.SendAsync(request, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = (await response.Content.ReadFromJsonAsync<MeDto>(Cancellation))!;
        me.Id.ShouldBe(user.Id);
        me.Email.ShouldBe(email);
        me.Roles.ShouldBe(["Customer"]);
    }

    [Fact]
    public async Task An_email_address_can_be_registered_only_once_whatever_its_case()
    {
        var email = BankingApiFactory.NewEmail();
        using var first = await RegisterAsync(email, Password);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var again = await RegisterAsync(email.ToUpperInvariant(), Password);

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BankingClient.ErrorCodeAsync(again)).ShouldBe("auth.email_taken");
    }

    [Fact]
    public async Task A_weak_password_is_rejected()
    {
        using var response = await RegisterAsync(BankingApiFactory.NewEmail(), "short");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("auth.password_too_weak");
    }

    [Fact]
    public async Task An_invalid_email_address_is_rejected()
    {
        using var response = await RegisterAsync("not-an-email-address", Password);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("auth.invalid_email");
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_email_get_the_same_answer()
    {
        var email = BankingApiFactory.NewEmail();
        using var registered = await RegisterAsync(email, Password);

        using var wrongPassword = await LoginAsync(email, "Wrong-Horse-99");
        using var unknownEmail = await LoginAsync(BankingApiFactory.NewEmail(), Password);

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await BankingClient.ErrorCodeAsync(wrongPassword)).ShouldBe("auth.invalid_credentials");
        (await BankingClient.ErrorCodeAsync(unknownEmail)).ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_against_the_right_one()
    {
        var email = BankingApiFactory.NewEmail();
        using var registered = await RegisterAsync(email, Password);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            using var wrong = await LoginAsync(email, "Wrong-Horse-99");
            (await BankingClient.ErrorCodeAsync(wrong)).ShouldBe("auth.invalid_credentials");
        }

        using var fifth = await LoginAsync(email, "Wrong-Horse-99");
        using var right = await LoginAsync(email, Password);

        (await BankingClient.ErrorCodeAsync(fifth)).ShouldBe("auth.locked_out");
        right.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await BankingClient.ErrorCodeAsync(right)).ShouldBe("auth.locked_out");
    }

    [Fact]
    public async Task Concurrent_registrations_for_one_email_create_one_user()
    {
        var email = BankingApiFactory.NewEmail();
        var statuses = new ConcurrentBag<HttpStatusCode>();

        // Identity checks the address in code first; requests that pass that check together race to insert.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 10),
            new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = Cancellation },
            async (_, _) =>
            {
                using var response = await RegisterAsync(email, Password);
                statuses.Add(response.StatusCode);
            });

        statuses.Count(status => status == HttpStatusCode.Created).ShouldBe(1);
        statuses.Count(status => status == HttpStatusCode.Conflict).ShouldBe(9);
    }

    private Task<HttpResponseMessage> RegisterAsync(string email, string password) =>
        _anonymous.PostAsJsonAsync("/auth/register", new { email, password }, Cancellation);

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _anonymous.PostAsJsonAsync("/auth/login", new { email, password }, Cancellation);
}
