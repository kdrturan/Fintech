using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Banking.IntegrationTests;

public sealed record AccountDto(Guid Id, string Currency, decimal Balance);

public sealed record TransferDto(Guid Id, Guid SourceAccountId, Guid DestinationAccountId, decimal Amount, string Currency, string Status, string? FailureReason);

/// <summary>Calls the API the way a client would. Every money-moving call sends a fresh Idempotency-Key unless one is given.</summary>
public sealed class BankingClient(HttpClient http)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public HttpClient Http => http;

    public async Task<AccountDto> OpenAccountAsync(string currency = "TRY")
    {
        using var response = await http.PostAsJsonAsync("/accounts", new { currency }, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AccountDto>(Cancellation))!;
    }

    public async Task<AccountDto> OpenAccountWithAsync(decimal balance, string currency = "TRY")
    {
        var account = await OpenAccountAsync(currency);
        using var response = await DepositAsync(account.Id, balance, currency);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return account;
    }

    public async Task<AccountDto> GetAccountAsync(Guid id) =>
        (await http.GetFromJsonAsync<AccountDto>($"/accounts/{id}", Cancellation))!;

    public async Task<decimal> BalanceAsync(Guid id) => (await GetAccountAsync(id)).Balance;

    public Task<HttpResponseMessage> DepositAsync(Guid accountId, decimal amount, string currency = "TRY", string? idempotencyKey = null) =>
        PostAsync($"/accounts/{accountId}/deposits", new { amount, currency }, idempotencyKey ?? NewKey());

    public Task<HttpResponseMessage> TransferAsync(Guid from, Guid to, decimal amount, string currency = "TRY", string? idempotencyKey = null) =>
        PostAsync("/transfers", new { sourceAccountId = from, destinationAccountId = to, amount, currency }, idempotencyKey ?? NewKey());

    public async Task<TransferDto> GetTransferAsync(Guid id) =>
        (await http.GetFromJsonAsync<TransferDto>($"/transfers/{id}", Cancellation))!;

    public static string NewKey() => Guid.NewGuid().ToString();

    public static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
    }

    public static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await ProblemAsync(response)).GetProperty("errorCode").GetString();

    private async Task<HttpResponseMessage> PostAsync(string path, object body, string idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await http.SendAsync(request, Cancellation);
    }
}
