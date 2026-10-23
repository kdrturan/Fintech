using Banking.Modules.Ledger.Domain;

namespace Banking.Modules.Ledger.Application.Abstractions;

public interface IAccountRepository
{
    /// <summary>Reads an account without locking it. A balance read this way is only for display, never for a decision.</summary>
    Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken);

    Task AddAsync(Account account, CancellationToken cancellationToken);
}
