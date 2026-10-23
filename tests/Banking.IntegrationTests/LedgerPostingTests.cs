using Banking.Modules.Ledger.Contracts;
using Banking.Modules.Ledger.Domain;
using Banking.Modules.Ledger.Infrastructure.Persistence;
using Banking.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.IntegrationTests;

/// <summary>The Ledger's posting transaction against a real database (docs/adr/0011).</summary>
public sealed class LedgerPostingTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    [Fact]
    public async Task A_transfer_moves_money_between_customer_accounts()
    {
        var ahmet = await api.OpenAccountWithAsync(100m);
        var ayse = await api.OpenAccountWithAsync(0m);

        var result = await api.TransferAsync(Guid.CreateVersion7(), ahmet, ayse, 30m);

        result.Succeeded.ShouldBeTrue();
        (await api.BalanceAsync(ahmet)).ShouldBe(70m);
        (await api.BalanceAsync(ayse)).ShouldBe(30m);
    }

    [Fact]
    public async Task Insufficient_funds_leave_nothing_behind_in_the_database()
    {
        var ahmet = await api.OpenAccountWithAsync(20m);
        var ayse = await api.OpenAccountWithAsync(0m);
        var key = Guid.CreateVersion7();

        var result = await api.TransferAsync(key, ahmet, ayse, 30m);

        result.ErrorCode.ShouldBe("ledger.insufficient_funds");
        (await api.BalanceAsync(ahmet)).ShouldBe(20m);
        (await api.BalanceAsync(ayse)).ShouldBe(0m);

        // The transaction was rolled back: not even the journal entry was saved.
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        (await db.JournalEntries.AnyAsync(entry => entry.IdempotencyKey == key, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task An_unknown_account_is_not_found()
    {
        var ahmet = await api.OpenAccountWithAsync(100m);

        var result = await api.TransferAsync(Guid.CreateVersion7(), ahmet, Guid.NewGuid(), 30m);

        result.ErrorCode.ShouldBe("ledger.account_not_found");
        result.ErrorType.ShouldBe(nameof(ErrorType.NotFound));
        (await api.BalanceAsync(ahmet)).ShouldBe(100m);
    }

    [Fact]
    public async Task The_banks_own_accounts_cannot_be_used_by_a_transfer()
    {
        var ahmet = await api.OpenAccountWithAsync(100m);

        var result = await api.TransferAsync(Guid.CreateVersion7(), ahmet, BankAccounts.Cash(Currency.TRY).Value, 30m);

        result.ErrorCode.ShouldBe("ledger.account_not_found");
    }

    [Fact]
    public async Task Several_posts_in_one_scope_each_lock_and_read_afresh()
    {
        var ahmet = await api.OpenAccountWithAsync(100m);
        var ayse = await api.OpenAccountWithAsync(0m);

        // The recovery job (phase 2.6) posts several transfers with one DbContext.
        await using var scope = api.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        var results = new List<LedgerPostResult>();
        for (var i = 0; i < 3; i++)
        {
            results.Add(await ledger.TransferAsync(Guid.CreateVersion7(), ahmet, ayse, 10m, Currency.TRY.Code, TestContext.Current.CancellationToken));
        }

        results.ShouldAllBe(result => result.Succeeded);
        (await api.BalanceAsync(ahmet)).ShouldBe(70m);
    }
}
