using Banking.Modules.Ledger.Domain;

namespace Banking.Modules.Ledger.Application.Abstractions;

public interface IAccountRepository
{
    /// <summary>Reads an account without locking it. A balance read this way is only for display, never for a decision.</summary>
    Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken);

    /// <summary>Every account of one customer, oldest first. The bank's own accounts have no owner, so they are never in it.</summary>
    Task<IReadOnlyList<Account>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken);

    Task AddAsync(Account account, CancellationToken cancellationToken);
}
