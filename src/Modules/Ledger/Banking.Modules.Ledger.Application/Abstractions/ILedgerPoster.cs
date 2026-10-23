using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;

namespace Banking.Modules.Ledger.Application.Abstractions;

/// <summary>
/// Applies a journal entry to the database in one transaction: locks the accounts, runs the
/// <see cref="PostingEngine"/> and saves the entry and the new balances, or changes nothing.
/// </summary>
public interface ILedgerPoster
{
    /// <summary>
    /// Posting an entry whose idempotency key was already posted returns the original entry's id and changes nothing.
    /// </summary>
    Task<Result<JournalEntryId>> PostAsync(JournalEntry entry, CancellationToken cancellationToken);
}
