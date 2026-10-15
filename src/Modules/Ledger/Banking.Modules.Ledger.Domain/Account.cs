using Banking.SharedKernel;

namespace Banking.Modules.Ledger.Domain;

/// <summary>
/// A ledger account in a single currency. The balance is stored for fast reads and locking,
/// but the postings are the source of truth; a reconciliation job checks they agree. See docs/adr/0009.
/// </summary>
public sealed class Account
{
    private Account(AccountId id, AccountType type, Currency currency, bool allowsNegativeBalance)
    {
        Id = id;
        Type = type;
        Currency = currency;
        AllowsNegativeBalance = allowsNegativeBalance;
        Balance = Money.Zero(currency);
    }

    public AccountId Id { get; }

    public AccountType Type { get; }

    public Currency Currency { get; }

    /// <summary>Customer accounts cannot go below zero (no overdraft yet); the bank's internal accounts can.</summary>
    public bool AllowsNegativeBalance { get; }

    public Money Balance { get; private set; }

    /// <summary>A customer's deposit account. From the bank's point of view it is a liability.</summary>
    public static Account OpenCustomerAccount(Currency currency) =>
        new(AccountId.New(), AccountType.Liability, currency, allowsNegativeBalance: false);

    /// <summary>One of the bank's own accounts, such as cash, clearing or fee income.</summary>
    public static Account OpenInternalAccount(AccountType type, Currency currency) =>
        new(AccountId.New(), type, currency, allowsNegativeBalance: true);

    /// <summary>How a posting changes this account's balance: +amount on its increasing side, -amount on the other.</summary>
    public Money EffectOf(Direction direction, Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        var increasesWithDebit = Type is AccountType.Asset or AccountType.Expense;
        var increases = increasesWithDebit == (direction == Direction.Debit);
        return increases ? amount : -amount;
    }
}
