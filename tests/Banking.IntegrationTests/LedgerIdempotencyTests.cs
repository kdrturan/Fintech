namespace Banking.IntegrationTests;

/// <summary>
/// The Ledger side of idempotency (docs/adr/0012): the same key is posted at most once, however often and
/// however concurrently it arrives. This is what makes the recovery job safe.
/// </summary>
public sealed class LedgerIdempotencyTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    [Fact]
    public async Task Posting_the_same_key_twice_moves_the_money_once()
    {
        var ahmet = await api.OpenAccountWithAsync(100m);
        var ayse = await api.OpenAccountWithAsync(0m);
        var key = Guid.CreateVersion7();

        var first = await api.TransferAsync(key, ahmet, ayse, 30m);
        var second = await api.TransferAsync(key, ahmet, ayse, 30m);

        first.Succeeded.ShouldBeTrue();
        second.Succeeded.ShouldBeTrue();
        (await api.BalanceAsync(ahmet)).ShouldBe(70m);
    }

    [Fact]
    public async Task Concurrent_posts_of_the_same_key_move_the_money_once()
    {
        var ahmet = await api.OpenAccountWithAsync(100m);
        var ayse = await api.OpenAccountWithAsync(0m);
        var key = Guid.CreateVersion7();

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => api.TransferAsync(key, ahmet, ayse, 30m)));

        results.ShouldAllBe(result => result.Succeeded);
        (await api.BalanceAsync(ahmet)).ShouldBe(70m);
        (await api.BalanceAsync(ayse)).ShouldBe(30m);
    }

    [Fact]
    public async Task The_same_key_on_different_accounts_is_still_posted_once()
    {
        var ahmet = await api.OpenAccountWithAsync(100m);
        var ayse = await api.OpenAccountWithAsync(0m);
        var mehmet = await api.OpenAccountWithAsync(100m);
        var zeynep = await api.OpenAccountWithAsync(0m);
        var key = Guid.CreateVersion7();

        // No shared account, so the row locks do not order these two; only the unique index stops the second.
        var results = await Task.WhenAll(api.TransferAsync(key, ahmet, ayse, 30m), api.TransferAsync(key, mehmet, zeynep, 30m));

        results.ShouldAllBe(result => result.Succeeded);
        ((await api.BalanceAsync(ayse)) + (await api.BalanceAsync(zeynep))).ShouldBe(30m);
    }
}
