using System.Collections.Concurrent;
using System.Net;

namespace Banking.IntegrationTests;

/// <summary>
/// The tests that prove the locking strategy (docs/adr/0011). Without the row locks the first test loses updates:
/// more than 50 transfers succeed and the balance no longer matches the money that left.
/// </summary>
public sealed class ConcurrencyTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private readonly BankingClient _client = new(api.CreateClient());

    [Fact]
    public async Task Concurrent_transfers_never_spend_more_than_the_balance()
    {
        var ahmet = await _client.OpenAccountWithAsync(50m);
        var ayse = await _client.OpenAccountAsync();
        var statuses = new ConcurrentBag<HttpStatusCode>();

        // 100 transfers of 1 TL from an account that holds 50 TL, many of them at the same moment.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            new ParallelOptions { MaxDegreeOfParallelism = 20, CancellationToken = TestContext.Current.CancellationToken },
            async (_, _) =>
            {
                using var response = await _client.TransferAsync(ahmet.Id, ayse.Id, 1m);
                statuses.Add(response.StatusCode);
            });

        statuses.Count(status => status == HttpStatusCode.Created).ShouldBe(50);
        statuses.Count(status => status == HttpStatusCode.UnprocessableEntity).ShouldBe(50);
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(0m);
        (await _client.BalanceAsync(ayse.Id)).ShouldBe(50m);
    }

    [Fact]
    public async Task Transfers_in_opposite_directions_do_not_deadlock()
    {
        var ahmet = await _client.OpenAccountWithAsync(1_000m);
        var ayse = await _client.OpenAccountWithAsync(1_000m);
        var statuses = new ConcurrentBag<HttpStatusCode>();

        // Half of the transfers lock Ahmet then Ayse, half Ayse then Ahmet, unless locks are taken in id order.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 60),
            new ParallelOptions { MaxDegreeOfParallelism = 20, CancellationToken = TestContext.Current.CancellationToken },
            async (i, _) =>
            {
                var (from, to) = i % 2 == 0 ? (ahmet.Id, ayse.Id) : (ayse.Id, ahmet.Id);
                using var response = await _client.TransferAsync(from, to, 1m);
                statuses.Add(response.StatusCode);
            });

        // A deadlock would surface as a 500: PostgreSQL aborts one of the transactions.
        statuses.ShouldAllBe(status => status == HttpStatusCode.Created);
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(1_000m);
        (await _client.BalanceAsync(ayse.Id)).ShouldBe(1_000m);
    }
}
