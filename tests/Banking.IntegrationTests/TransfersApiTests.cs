using System.Net;
using System.Net.Http.Json;

namespace Banking.IntegrationTests;

public sealed class TransfersApiTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private readonly BankingClient _client = api.CreateBankingClient();

    [Fact]
    public async Task A_transfer_moves_money_and_completes()
    {
        var ahmet = await _client.OpenAccountWithAsync(100m);
        var ayse = await _client.OpenAccountAsync();

        using var response = await _client.TransferAsync(ahmet.Id, ayse.Id, 30m);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var transfer = (await response.Content.ReadFromJsonAsync<TransferDto>(TestContext.Current.CancellationToken))!;
        transfer.Status.ShouldBe("Completed");
        response.Headers.Location.ShouldNotBeNull().AbsolutePath.ShouldBe($"/transfers/{transfer.Id}");
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(70m);
        (await _client.BalanceAsync(ayse.Id)).ShouldBe(30m);
    }

    [Fact]
    public async Task Insufficient_funds_fail_the_transfer_and_move_nothing()
    {
        var ahmet = await _client.OpenAccountWithAsync(20m);
        var ayse = await _client.OpenAccountAsync();

        using var response = await _client.TransferAsync(ahmet.Id, ayse.Id, 30m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = await BankingClient.ProblemAsync(response);
        problem.GetProperty("errorCode").GetString().ShouldBe("ledger.insufficient_funds");

        // The attempt is kept, so it can be looked up and explained.
        var transfer = await _client.GetTransferAsync(problem.GetProperty("transferId").GetGuid());
        transfer.Status.ShouldBe("Failed");
        transfer.FailureReason.ShouldBe("ledger.insufficient_funds");
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(20m);
        (await _client.BalanceAsync(ayse.Id)).ShouldBe(0m);
    }

    [Fact]
    public async Task A_transfer_to_the_same_account_creates_nothing()
    {
        var ahmet = await _client.OpenAccountWithAsync(100m);

        using var response = await _client.TransferAsync(ahmet.Id, ahmet.Id, 30m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("transfers.same_account");
    }

    [Fact]
    public async Task A_transfer_to_an_unknown_account_fails()
    {
        var ahmet = await _client.OpenAccountWithAsync(100m);

        using var response = await _client.TransferAsync(ahmet.Id, Guid.NewGuid(), 30m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("ledger.account_not_found");
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(100m);
    }

    [Fact]
    public async Task A_transfer_request_without_an_amount_is_a_validation_error()
    {
        var ahmet = await _client.OpenAccountAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/transfers")
        {
            Content = JsonContent.Create(new { sourceAccountId = ahmet.Id, destinationAccountId = Guid.NewGuid(), currency = "TRY" }),
        };
        request.Headers.Add("Idempotency-Key", BankingClient.NewKey());

        using var response = await _client.Http.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_transfer_is_not_found()
    {
        using var response = await _client.Http.GetAsync($"/transfers/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("transfers.not_found");
    }
}
