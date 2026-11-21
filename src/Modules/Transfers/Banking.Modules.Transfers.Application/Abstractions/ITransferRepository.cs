using Banking.Modules.Transfers.Domain;

namespace Banking.Modules.Transfers.Application.Abstractions;

public interface ITransferRepository
{
    Task AddAsync(Transfer transfer, CancellationToken cancellationToken);

    Task<Transfer?> FindAsync(TransferId id, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a status change. Returns false if someone else (the recovery job) changed the transfer
    /// since it was read; the change is then discarded and the caller reads the transfer again.
    /// </summary>
    Task<bool> TrySaveAsync(Transfer transfer, CancellationToken cancellationToken);
}
