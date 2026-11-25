using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Banking.IntegrationTests;

/// <summary>
/// The real API, in memory, on a database of its own. Used as a class fixture: one per test class.
/// The recovery job's background loop is off; tests run a recovery round themselves when they need one.
/// Every client it creates is signed in as the class's own customer, unless it asks to be anonymous.
/// </summary>
public sealed class BankingApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Correct-Horse-9";

    public const string TellerEmail = "teller@example.com";

    /// <summary>
    /// The key the API signs tokens with in tests: made fresh for every test run, never stored anywhere.
    /// Tests use it to build tokens of their own, valid or deliberately broken. Kept as parameters, not as an RSA
    /// object: test classes run in parallel, and an RSA object used for the first time by several threads can fail.
    /// </summary>
    public static RSAParameters SigningKey { get; } = CreateKey();

    private string? _accessToken;

    private string? _tellerToken;

    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>The user every client of this factory is signed in as.</summary>
    public Guid CustomerId { get; private set; }

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await postgres.CreateDatabaseAsync();
        (CustomerId, _accessToken) = await RegisterAndSignInAsync(NewEmail());
        _tellerToken = await SignInAsync(TellerEmail);
    }

    /// <summary>The class's customer, with a teller at hand for paying cash in.</summary>
    public BankingClient CreateBankingClient() => new(CreateClient(), CreateTellerClient());

    /// <summary>Another customer, for testing that one customer cannot reach what belongs to another.</summary>
    public async Task<(Guid UserId, BankingClient Client)> CreateOtherCustomerAsync()
    {
        var (userId, accessToken) = await RegisterAndSignInAsync(NewEmail());
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return (userId, new BankingClient(client, CreateTellerClient()));
    }

    /// <summary>A client signed in as the teller that the API created from its configuration when it started.</summary>
    public HttpClient CreateTellerClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tellerToken);
        return client;
    }

    /// <summary>A client without a token, for the endpoints anyone may call and for testing what happens without one.</summary>
    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }

    public async Task<(Guid UserId, string AccessToken)> RegisterAndSignInAsync(string email)
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var client = CreateAnonymousClient();

        using var registered = await client.PostAsJsonAsync("/auth/register", new { email, password = Password }, cancellation);
        registered.StatusCode.ShouldBe(HttpStatusCode.Created);
        var user = (await registered.Content.ReadFromJsonAsync<RegisteredUserDto>(cancellation))!;

        return (user.Id, await SignInAsync(email));
    }

    private async Task<string> SignInAsync(string email)
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var client = CreateAnonymousClient();

        using var signedIn = await client.PostAsJsonAsync("/auth/login", new { email, password = Password }, cancellation);
        signedIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await signedIn.Content.ReadFromJsonAsync<LoginDto>(cancellation))!.AccessToken;
    }

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Banking", ConnectionString);
        builder.UseSetting("TransferRecovery:Enabled", "false");
        builder.UseSetting("SeedTeller:Email", TellerEmail);
        builder.UseSetting("SeedTeller:Password", Password);
        using var rsa = RSA.Create(SigningKey);
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()));
    }

    private static RSAParameters CreateKey()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportParameters(includePrivateParameters: true);
    }

    protected override void ConfigureClient(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        base.ConfigureClient(client);

        if (_accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync() => await base.DisposeAsync();
}
