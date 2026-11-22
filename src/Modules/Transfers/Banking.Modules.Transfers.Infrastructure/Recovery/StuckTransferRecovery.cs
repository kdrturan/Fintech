using Banking.Modules.Ledger.Contracts;
using Banking.Modules.Transfers.Application;
using Banking.Modules.Transfers.Domain;
using Banking.Modules.Transfers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Banking.Modules.Transfers.Infrastructure.Recovery;

/// <summary>
/// Finishes transfers that a crash left in Pending (docs/adr/0003, docs/adr/0014): posts them to the Ledger again with the same
/// key, which either moves the money or returns the original result, then records the outcome.
/// Only Pending transfers are picked up; Completed and Failed are final and never retried.
/// </summary>
public sealed class StuckTransferRecovery(
    TransfersDbContext db,
    ILedgerApi ledger,
    IOptions<TransferRecoveryOptions> options,
    TimeProvider time)
{
    /// <returns>How many transfers were finished.</returns>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var cutoff = time.GetUtcNow() - options.Value.StuckAfter;
        var pending = nameof(TransferStatus.Pending);
        var batchSize = options.Value.BatchSize;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // FOR UPDATE: a request finishing the same transfer waits for us and then sees our outcome.
        // SKIP LOCKED: a second instance of this job takes other transfers instead of waiting for ours.
        var lockedIds = await db.Database
            .SqlQuery<Guid>($"""
                SELECT id AS "Value" FROM transfers.transfers
                WHERE status = {pending} AND created_at < {cutoff}
                ORDER BY created_at
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        // The rows are ours until commit; load them the normal way.
        var ids = lockedIds.Select(id => new TransferId(id)).ToList();
        var stuck = await db.Transfers.Where(transfer => ids.Contains(transfer.Id)).ToListAsync(cancellationToken);

        foreach (var transfer in stuck)
        {
            var posted = await TransferSettlement.PostAsync(ledger, transfer, cancellationToken);
            TransferSettlement.Record(transfer, posted);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return stuck.Count;
    }
}
