using Banking.Modules.Transfers.Application.Abstractions;
using Banking.Modules.Transfers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Banking.Modules.Transfers.Infrastructure.Persistence;

internal sealed class TransferRepository(TransfersDbContext db) : ITransferRepository
{
    public async Task AddAsync(Transfer transfer, CancellationToken cancellationToken)
    {
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Transfer?> FindAsync(TransferId id, CancellationToken cancellationToken)
    {
        // A transfer that is already tracked could hold a stale status; always read the stored one.
        var tracked = db.ChangeTracker.Entries<Transfer>().FirstOrDefault(entry => entry.Entity.Id == id);
        if (tracked is not null)
        {
            tracked.State = EntityState.Detached;
        }

        return await db.Transfers.FirstOrDefaultAsync(transfer => transfer.Id == id, cancellationToken);
    }

    public async Task<bool> TrySaveAsync(Transfer transfer, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(transfer).State = EntityState.Detached;
            return false;
        }
    }
}
