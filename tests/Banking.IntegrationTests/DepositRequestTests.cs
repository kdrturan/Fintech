using System.Net;
using System.Net.Http.Json;

namespace Banking.IntegrationTests;

public sealed class DepositRequestTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private readonly BankingClient _client = new(api.CreateClient());

    [Fact]
    public async Task A_deposit_without_an_amount_is_a_validation_error()
    {
        var account = await _client.OpenAccountAsync();

        // With a key: without one, the idempotency filter answers 400 key_missing before model validation runs.
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/accounts/{account.Id}/deposits")
        {
            Content = JsonContent.Create(new { currency = "TRY" }),
        };
        request.Headers.Add("Idempotency-Key", BankingClient.NewKey());

        using var response = await _client.Http.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await BankingClient.ProblemAsync(response);
        problem.GetProperty("errors").TryGetProperty("Amount", out _).ShouldBeTrue();
        (await _client.BalanceAsync(account.Id)).ShouldBe(0m);
    }

    [Fact]
    public async Task A_deposit_to_an_unknown_account_is_not_found()
    {
        using var response = await _client.DepositAsync(Guid.NewGuid(), 10m);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("ledger.account_not_found");
    }
}
