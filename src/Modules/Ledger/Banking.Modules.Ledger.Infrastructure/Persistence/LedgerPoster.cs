using Banking.Modules.Ledger.Application.Abstractions;
using Banking.Modules.Ledger.Domain;
using Banking.Modules.Ledger.Infrastructure.Persistence.Configurations;
using Banking.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Banking.Modules.Ledger.Infrastructure.Persistence;

/// <summary>
/// Posts a journal entry in one database transaction. The accounts are locked first, so two concurrent
/// entries on the same account run one after the other and the second one sees the first one's balance.
/// See docs/adr/0011.
/// </summary>
internal sealed class LedgerPoster(LedgerDbContext db) : ILedgerPoster
{
    public async Task<Result<JournalEntryId>> PostAsync(JournalEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            return await PostInTransactionAsync(entry, cancellationToken);
        }
        finally
        {
            // Leave nothing tracked: the next post in the same scope (the recovery job posts several) must lock afresh.
            db.ChangeTracker.Clear();
        }
    }

    private async Task<Result<JournalEntryId>> PostInTransactionAsync(JournalEntry entry, CancellationToken cancellationToken)
    {
        // An account loaded earlier in this context would be returned as-is by the query after the lock,
        // with the balance it had before the lock: exactly the lost update the lock is there to prevent.
        if (db.ChangeTracker.Entries<Account>().Any())
        {
            throw new InvalidOperationException("Accounts must not be tracked before they are locked for posting. Read them with AsNoTracking.");
        }

        // Always lock in id order: two entries that touch the same accounts then wait for each other
        // in the same order and cannot deadlock.
        var ids = entry.Postings.Select(posting => posting.AccountId.Value).Distinct().Order().ToArray();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.Database
            .SqlQuery<Guid>($"""SELECT id AS "Value" FROM ledger.accounts WHERE id = ANY({ids}) ORDER BY id FOR UPDATE""")
            .ToListAsync(cancellationToken);

        // Locked until commit, so this read sees the latest balances and nobody can change them before we do.
        var accountIds = ids.Select(id => new AccountId(id)).ToList();
        var accounts = await db.Accounts
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        // Checked after the lock: a concurrent post of the same key that held the lock has committed by now.
        var alreadyPosted = await FindByIdempotencyKeyAsync(entry.IdempotencyKey, cancellationToken);
        if (alreadyPosted is { } existingId)
        {
            return existingId;
        }

        var missing = ids.Select(id => new AccountId(id)).FirstOrDefault(id => !accounts.ContainsKey(id));
        if (missing != default)
        {
            return LedgerErrors.AccountNotFound(missing);
        }

        var result = PostingEngine.Post(entry, accounts);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        db.JournalEntries.Add(entry);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return entry.Id;
        }
        catch (DbUpdateException exception) when (IsDuplicateIdempotencyKey(exception))
        {
            // The same key was posted by a request that did not touch the same accounts, so the lock
            // did not order the two. The unique index is the last line of defence.
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return await FindByIdempotencyKeyAsync(entry.IdempotencyKey, cancellationToken)
                ?? throw new InvalidOperationException("The idempotency key was reported as a duplicate but cannot be found.", exception);
        }
    }

    private async Task<JournalEntryId?> FindByIdempotencyKeyAsync(Guid key, CancellationToken cancellationToken)
    {
        var ids = await db.JournalEntries
            .AsNoTracking()
            .Where(entry => entry.IdempotencyKey == key)
            .Select(entry => entry.Id)
            .ToListAsync(cancellationToken);

        return ids.Count == 0 ? null : ids[0];
    }

    private static bool IsDuplicateIdempotencyKey(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: JournalEntryConfiguration.IdempotencyKeyIndex,
        };
}
