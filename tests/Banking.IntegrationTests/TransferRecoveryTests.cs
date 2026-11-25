using Banking.Modules.Ledger.Contracts;
using Banking.Modules.Transfers.Domain;
using Banking.Modules.Transfers.Infrastructure.Persistence;
using Banking.Modules.Transfers.Infrastructure.Recovery;
using Banking.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.IntegrationTests;

/// <summary>
/// A crash can leave a transfer Pending at two points (docs/adr/0003): before the Ledger posted it, or after.
/// The recovery job must finish both, and must move the money exactly once.
/// </summary>
public sealed class TransferRecoveryTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private static readonly TimeSpan LongAgo = TimeSpan.FromHours(1);

    private readonly BankingClient _client = api.CreateBankingClient();

    [Fact]
    public async Task A_transfer_stuck_before_posting_is_completed_and_the_money_moves()
    {
        var ahmet = await _client.OpenAccountWithAsync(100m);
        var ayse = await _client.OpenAccountAsync();
        var transfer = await SavePendingTransferAsync(ahmet.Id, ayse.Id, 30m, createdAgo: LongAgo);

        await RunRecoveryAsync();

        (await _client.GetTransferAsync(transfer.Id.Value)).Status.ShouldBe("Completed");
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(70m);
        (await _client.BalanceAsync(ayse.Id)).ShouldBe(30m);
    }

    [Fact]
    public async Task A_transfer_stuck_after_posting_is_completed_without_moving_the_money_again()
    {
        var ahmet = await _client.OpenAccountWithAsync(100m);
        var ayse = await _client.OpenAccountAsync();
        var transfer = await SavePendingTransferAsync(ahmet.Id, ayse.Id, 30m, createdAgo: LongAgo);

        // The crash: the Ledger posted, but the process died before the transfer was marked Completed.
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
            (await ledger.TransferAsync(transfer.Id.Value, api.CustomerId, ahmet.Id, ayse.Id, 30m, "TRY", TestContext.Current.CancellationToken))
                .Succeeded.ShouldBeTrue();
        }

        await RunRecoveryAsync();

        (await _client.GetTransferAsync(transfer.Id.Value)).Status.ShouldBe("Completed");
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(70m);
    }

    [Fact]
    public async Task Several_stuck_transfers_are_finished_in_one_round()
    {
        var ahmet = await _client.OpenAccountWithAsync(100m);
        var ayse = await _client.OpenAccountAsync();
        var transfers = new List<Transfer>();
        for (var i = 0; i < 3; i++)
        {
            transfers.Add(await SavePendingTransferAsync(ahmet.Id, ayse.Id, 10m, createdAgo: LongAgo));
        }

        await RunRecoveryAsync();

        foreach (var transfer in transfers)
        {
            (await _client.GetTransferAsync(transfer.Id.Value)).Status.ShouldBe("Completed");
        }

        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(70m);
    }

    [Fact]
    public async Task A_failed_transfer_is_final_and_never_retried()
    {
        var ahmet = await _client.OpenAccountWithAsync(10m);
        var ayse = await _client.OpenAccountAsync();
        var transfer = await SavePendingTransferAsync(ahmet.Id, ayse.Id, 30m, createdAgo: LongAgo, failWith: "ledger.insufficient_funds");

        // Ahmet could now pay. Retrying would silently turn a rejected transfer into a completed one.
        using var deposit = await _client.DepositAsync(ahmet.Id, 100m);
        await RunRecoveryAsync();

        (await _client.GetTransferAsync(transfer.Id.Value)).Status.ShouldBe("Failed");
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(110m);
    }

    [Fact]
    public async Task A_young_pending_transfer_is_left_to_its_request()
    {
        var ahmet = await _client.OpenAccountWithAsync(100m);
        var ayse = await _client.OpenAccountAsync();
        var transfer = await SavePendingTransferAsync(ahmet.Id, ayse.Id, 30m, createdAgo: TimeSpan.Zero);

        await RunRecoveryAsync();

        (await _client.GetTransferAsync(transfer.Id.Value)).Status.ShouldBe("Pending");
        (await _client.BalanceAsync(ahmet.Id)).ShouldBe(100m);
    }

    private async Task<Transfer> SavePendingTransferAsync(Guid from, Guid to, decimal amount, TimeSpan createdAgo, string? failWith = null)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransfersDbContext>();

        var transfer = Transfer.Create(api.CustomerId, from, to, Money.Of(amount, Currency.TRY)).Value;
        if (failWith is not null)
        {
            transfer.Fail(failWith);
        }

        db.Transfers.Add(transfer);
        db.Entry(transfer).Property(TransferConfigurationNames.CreatedAt).CurrentValue = DateTimeOffset.UtcNow - createdAgo;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return transfer;
    }

    private async Task RunRecoveryAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<StuckTransferRecovery>().RunOnceAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The shadow property's name; the configuration class itself is internal to the module.</summary>
    private static class TransferConfigurationNames
    {
        public const string CreatedAt = "CreatedAt";
    }
}
