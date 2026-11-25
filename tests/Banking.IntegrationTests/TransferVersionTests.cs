using Banking.Modules.Transfers.Application.Abstractions;
using Banking.Modules.Transfers.Domain;
using Banking.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.IntegrationTests;

/// <summary>
/// Proves the optimistic concurrency check on a transfer's status (xmin). No HTTP: today only the request itself
/// changes a transfer, so the test plays the second writer (the recovery job, 2.6) with a second DI scope.
/// </summary>
public sealed class TransferVersionTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    [Fact]
    public async Task A_stale_save_loses_and_the_stored_outcome_stands()
    {
        var cancellation = TestContext.Current.CancellationToken;

        // Plain ids: the transfers table has no foreign key to the Ledger's accounts, and nothing here calls the Ledger.
        var transfer = Transfer.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Money.TryCreate(10m, "TRY").Value).Value;

        // Scope 1, the request: records the intent. Its DbContext now holds this transfer, with the xmin it saw.
        await using var first = api.Services.CreateAsyncScope();
        var firstRepository = first.ServiceProvider.GetRequiredService<ITransferRepository>();
        await firstRepository.AddAsync(transfer, cancellation);

        // Scope 2, the other writer: reads the same row and completes it. The row's xmin changes.
        await using (var second = api.Services.CreateAsyncScope())
        {
            var secondRepository = second.ServiceProvider.GetRequiredService<ITransferRepository>();
            var current = (await secondRepository.FindAsync(transfer.Id, cancellation)).ShouldNotBeNull();
            current.Complete();
            (await secondRepository.TrySaveAsync(current, cancellation)).ShouldBeTrue();
        }

        // Back in scope 1, with the stale copy: its UPDATE ... WHERE xmin = <old> matches no row, so it is rejected.
        transfer.Fail("ledger.insufficient_funds");
        (await firstRepository.TrySaveAsync(transfer, cancellation)).ShouldBeFalse();

        // FindAsync always reads the stored row, so even scope 1 sees the outcome that won.
        var stored = (await firstRepository.FindAsync(transfer.Id, cancellation)).ShouldNotBeNull();
        stored.Status.ShouldBe(TransferStatus.Completed);
        stored.FailureReason.ShouldBeNull();
    }
}
