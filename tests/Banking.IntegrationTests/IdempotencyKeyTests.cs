using System.Collections.Concurrent;
using System.Net;

namespace Banking.IntegrationTests;

/// <summary>The HTTP side of idempotency (docs/adr/0012): what a client sees when it retries a request.</summary>
public sealed class IdempotencyKeyTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private readonly BankingClient _client = new(api.CreateClient());

    [Fact]
    public async Task A_retry_with_the_same_key_returns_the_first_response_and_moves_the_money_once()
    {
        var ahmet = await _client.OpenAccountWithAsync(100m);
        var ayse = await _client.OpenAccountAsync();
        var key = BankingClient.NewKey();

        using var first = await _client.TransferAsync(ahmet.Id, ayse.Id, 30m, idempotencyKey: key);
        using var retry = await _client.TransferAsync(ahmet.Id, ayse.Id, 30m, idempotencyKey: key);

        retry.StatusCode.ShouldBe(first.StatusCode);
        (await retry.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        retry.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(70m);
    }

    [Fact]
    public async Task A_failed_request_is_replayed_as_failed_even_if_it_would_now_succeed()
    {
        var ahmet = await _client.OpenAccountWithAsync(10m);
        var ayse = await _client.OpenAccountAsync();
        var key = BankingClient.NewKey();

        using var first = await _client.TransferAsync(ahmet.Id, ayse.Id, 30m, idempotencyKey: key);
        using var deposit = await _client.DepositAsync(ahmet.Id, 100m);
        using var retry = await _client.TransferAsync(ahmet.Id, ayse.Id, 30m, idempotencyKey: key);

        // The key stands for one attempt and its outcome. A new attempt needs a new key.
        first.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        retry.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(110m);
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_request_is_rejected()
    {
        var ahmet = await _client.OpenAccountWithAsync(100m);
        var ayse = await _client.OpenAccountAsync();
        var key = BankingClient.NewKey();

        using var first = await _client.TransferAsync(ahmet.Id, ayse.Id, 30m, idempotencyKey: key);
        using var different = await _client.TransferAsync(ahmet.Id, ayse.Id, 31m, idempotencyKey: key);

        different.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await BankingClient.ErrorCodeAsync(different)).ShouldBe("idempotency.key_reused");
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(70m);
    }

    [Fact]
    public async Task A_money_moving_request_without_a_key_is_rejected()
    {
        var ahmet = await _client.OpenAccountAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/accounts/{ahmet.Id}/deposits")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { amount = 10m, currency = "TRY" }),
        };

        using var response = await _client.Http.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("idempotency.key_missing");
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(0m);
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_move_the_money_once()
    {
        var ahmet = await _client.OpenAccountWithAsync(100m);
        var ayse = await _client.OpenAccountAsync();
        var key = BankingClient.NewKey();
        var statuses = new ConcurrentBag<HttpStatusCode>();

        // A double-click: the same request several times at once. One runs; the others see it in progress or replay it.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 10),
            new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = TestContext.Current.CancellationToken },
            async (_, _) =>
            {
                using var response = await _client.TransferAsync(ahmet.Id, ayse.Id, 30m, idempotencyKey: key);
                statuses.Add(response.StatusCode);
            });

        statuses.ShouldAllBe(status => status == HttpStatusCode.Created || status == HttpStatusCode.Conflict);
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(70m);
        (await _client.BalanceAsync(ayse.Id)).ShouldBe(30m);
    }
}
