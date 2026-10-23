using Banking.Modules.Ledger.Application.Abstractions;
using Banking.Modules.Ledger.Domain;
using Microsoft.EntityFrameworkCore;

namespace Banking.Modules.Ledger.Infrastructure.Persistence;

internal sealed class AccountRepository(LedgerDbContext db) : IAccountRepository
{
    // AsNoTracking: an account read here must not be the instance the poster later locks and changes.
    public Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken) =>
        db.Accounts.AsNoTracking().FirstOrDefaultAsync(account => account.Id == id, cancellationToken);

    public async Task AddAsync(Account account, CancellationToken cancellationToken)
    {
        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        db.Entry(account).State = EntityState.Detached;
    }
}
