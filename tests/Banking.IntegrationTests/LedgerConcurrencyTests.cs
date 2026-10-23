using System.Collections.Concurrent;
using Banking.Modules.Ledger.Contracts;

namespace Banking.IntegrationTests;

public sealed class LedgerConcurrencyTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    [Fact]
    public async Task Concurrent_transfers_never_spend_more_than_the_balance()
    {
        var ahmet = await api.OpenAccountWithAsync(50m);
        var ayse = await api.OpenAccountWithAsync(0m);
        var results = new ConcurrentBag<LedgerPostResult>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            new ParallelOptions { MaxDegreeOfParallelism = 20, CancellationToken = TestContext.Current.CancellationToken },
            async (_, _) => results.Add(await api.TransferAsync(Guid.CreateVersion7(), ahmet, ayse, 1m)));

        results.Count(result => result.Succeeded).ShouldBe(50);
        results.Where(result => !result.Succeeded).ShouldAllBe(result => result.ErrorCode == "ledger.insufficient_funds");
        (await api.BalanceAsync(ahmet)).ShouldBe(0m);
        (await api.BalanceAsync(ayse)).ShouldBe(50m);
    }

    [Fact]
    public async Task Transfers_in_opposite_directions_do_not_deadlock()
    {
        var ahmet = await api.OpenAccountWithAsync(1000m);
        var ayse = await api.OpenAccountWithAsync(1000m);
        var results = new ConcurrentBag<LedgerPostResult>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 60),
            new ParallelOptions { MaxDegreeOfParallelism = 20, CancellationToken = TestContext.Current.CancellationToken },
            async (i, _) =>
            {
                var (from, to) = i % 2 == 0 ? (ahmet, ayse) : (ayse, ahmet);
                results.Add(await api.TransferAsync(Guid.CreateVersion7(), from, to, 1m));
            });

        results.Count(result => result.Succeeded).ShouldBe(60);
        (await api.BalanceAsync(ahmet)).ShouldBe(1000m);
        (await api.BalanceAsync(ayse)).ShouldBe(1000m);
    }
}
