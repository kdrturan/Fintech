using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;

namespace Banking.Modules.Ledger.Application.Abstractions;

/// <summary>Reads what happened on one account: its side of every journal entry that touched it.</summary>
public interface IAccountEntryReader
{
    /// <summary>Newest first, at most <paramref name="count"/>, and only those older than <paramref name="before"/> when it is given.</summary>
    Task<IReadOnlyList<AccountEntry>> ListAsync(AccountId accountId, long? before, int count, CancellationToken cancellationToken);
}

/// <summary>
/// One posting on an account, with what its journal entry says about it. <paramref name="Position"/> follows the order
/// the account's balance changed in: it is assigned while the account is locked, so a later change always gets a higher one.
/// </summary>
public sealed record AccountEntry(
    long Position,
    JournalEntryId EntryId,
    Guid OperationId,
    Direction Direction,
    Money Amount,
    DateTimeOffset CreatedAt);
