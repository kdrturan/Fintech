using Banking.SharedKernel;

namespace Banking.Modules.Ledger.Domain;

/// <summary>
/// The only code in the system that changes a balance. It applies a journal entry all or nothing:
/// every check runs before the first balance changes, so a failed entry leaves every account untouched.
/// </summary>
public static class PostingEngine
{
    /// <param name="entry">A balanced journal entry.</param>
    /// <param name="accounts">Every account the entry touches, loaded by the caller.</param>
    public static Result Post(JournalEntry entry, IReadOnlyDictionary<AccountId, Account> accounts)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(accounts);

        var newBalances = new Dictionary<AccountId, Money>();
        foreach (var posting in entry.Postings)
        {
            // A missing account means the caller loaded the wrong data: a bug, not a business outcome.
            if (!accounts.TryGetValue(posting.AccountId, out var account))
            {
                throw new ArgumentException($"Account {posting.AccountId} was not provided.", nameof(accounts));
            }

            if (account.Currency != posting.Amount.Currency)
            {
                return LedgerErrors.CurrencyMismatch(account.Id, account.Currency, posting.Amount.Currency);
            }

            var current = newBalances.GetValueOrDefault(account.Id, account.Balance);
            newBalances[account.Id] = current + account.EffectOf(posting.Direction, posting.Amount);
        }

        foreach (var (accountId, newBalance) in newBalances)
        {
            if (newBalance.IsNegative && !accounts[accountId].AllowsNegativeBalance)
            {
                return LedgerErrors.InsufficientFunds(accountId);
            }
        }

        foreach (var posting in entry.Postings)
        {
            accounts[posting.AccountId].Apply(posting.Direction, posting.Amount);
        }

        return Result.Success();
    }
}
