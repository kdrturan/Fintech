using System.Net;
using Banking.Modules.Ledger.Contracts;
using Banking.Modules.Transfers.Domain;
using Banking.Modules.Transfers.Infrastructure.Persistence;
using Banking.Modules.Transfers.Infrastructure.Recovery;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.IntegrationTests;

/// <summary>
/// The worst crash: the Ledger moved the money, then the request failed before the transfer was marked Completed.
/// All three layers must hold together: the Idempotency-Key stops a retry from creating a second transfer, the
/// recovery job finishes the stuck one, and the Ledger's own key stops it from moving the money again.
/// </summary>
public sealed class CrashAfterPostingTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    [Fact]
    public async Task A_retry_after_a_crash_moves_no_money_and_recovery_finishes_the_transfer()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var client = new BankingClient(api.CreateClient());
        var ahmet = await client.OpenAccountWithAsync(100m);
        var ayse = await client.OpenAccountAsync();

        // The same API and database, except that the Ledger "crashes" right after it moved the money.
        await using var crashing = api.WithWebHostBuilder(builder => builder.ConfigureTestServices(CrashAfterPosting));
        var crashingClient = new BankingClient(crashing.CreateClient());
        var key = BankingClient.NewKey();

        using var first = await crashingClient.TransferAsync(ahmet.Id, ayse.Id, 30m, idempotencyKey: key);
        using var retry = await crashingClient.TransferAsync(ahmet.Id, ayse.Id, 30m, idempotencyKey: key);

        first.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        retry.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        retry.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await client.BalanceAsync(ahmet.Id)).ShouldBe(70m);

        // One transfer, stuck in Pending. Age it past StuckAfter.
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TransfersDbContext>();
            var stuck = await db.Transfers.AsNoTracking().Where(transfer => transfer.SourceAccountId == ahmet.Id).ToListAsync(cancellation);
            stuck.ShouldHaveSingleItem().Status.ShouldBe(TransferStatus.Pending);

            await db.Database.ExecuteSqlAsync(
                $"UPDATE transfers.transfers SET created_at = now() - interval '1 hour' WHERE source_account_id = {ahmet.Id}",
                cancellation);
        }

        // A round of its own, in its own scope, as the background service runs it.
        await using (var scope = api.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<StuckTransferRecovery>().RunOnceAsync(cancellation)).ShouldBe(1);
        }

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TransfersDbContext>();
            var finished = await db.Transfers.SingleAsync(transfer => transfer.SourceAccountId == ahmet.Id, cancellation);
            finished.Status.ShouldBe(TransferStatus.Completed);
        }

        (await client.BalanceAsync(ahmet.Id)).ShouldBe(70m);
        (await client.BalanceAsync(ayse.Id)).ShouldBe(30m);
    }

    /// <summary>Wraps the real Ledger so that it throws after every successful posting.</summary>
    private static void CrashAfterPosting(IServiceCollection services)
    {
        var original = services.Single(descriptor => descriptor.ServiceType == typeof(ILedgerApi));
        var implementation = original.ImplementationType!;
        services.Remove(original);
        services.Add(new ServiceDescriptor(implementation, implementation, original.Lifetime));
        services.Add(new ServiceDescriptor(
            typeof(ILedgerApi),
            provider => new CrashingLedgerApi((ILedgerApi)provider.GetRequiredService(implementation)),
            original.Lifetime));
    }

    private sealed class CrashingLedgerApi(ILedgerApi inner) : ILedgerApi
    {
        public async Task<LedgerPostResult> TransferAsync(
            Guid idempotencyKey,
            Guid sourceAccountId,
            Guid destinationAccountId,
            decimal amount,
            string currency,
            CancellationToken cancellationToken)
        {
            var result = await inner.TransferAsync(idempotencyKey, sourceAccountId, destinationAccountId, amount, currency, cancellationToken);
            return result.Succeeded ? throw new InvalidOperationException("Simulated crash after the Ledger posted.") : result;
        }
    }
}
