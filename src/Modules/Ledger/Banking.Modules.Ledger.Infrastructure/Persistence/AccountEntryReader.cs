using Banking.Modules.Ledger.Application.Abstractions;
using Banking.Modules.Ledger.Domain;
using Microsoft.EntityFrameworkCore;

namespace Banking.Modules.Ledger.Infrastructure.Persistence;

internal sealed class AccountEntryReader(LedgerDbContext db) : IAccountEntryReader
{
    public async Task<IReadOnlyList<AccountEntry>> ListAsync(
        AccountId accountId,
        long? before,
        int count,
        CancellationToken cancellationToken)
    {
        var postings = db.Set<Posting>().AsNoTracking().Where(posting => posting.AccountId == accountId);

        // Added only when there is a cursor. "@before IS NULL OR id < @before" in one query can make PostgreSQL read
        // everything newer than the cursor first, instead of starting at the cursor.
        if (before is { } position)
        {
            postings = postings.Where(posting => EF.Property<long>(posting, "Id") < position);
        }

        // The sort key is the posting's id alone, because (account_id, id) is an index: PostgreSQL can walk it backwards
        // from the cursor and stop after "count" rows, however long the account's history is.
        var query =
            from posting in postings
            join entry in db.JournalEntries on EF.Property<JournalEntryId>(posting, "JournalEntryId") equals entry.Id
            orderby EF.Property<long>(posting, "Id") descending
            select new AccountEntry(
                EF.Property<long>(posting, "Id"),
                entry.Id,
                entry.IdempotencyKey,
                posting.Direction,
                posting.Amount,
                EF.Property<DateTimeOffset>(entry, "CreatedAt"));

        return await query.Take(count).ToListAsync(cancellationToken);
    }
}
