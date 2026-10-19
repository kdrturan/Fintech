using Banking.SharedKernel;

namespace Banking.Modules.Ledger.Domain;

/// <summary>One line of a journal entry: which account, which side, how much. The amount is always positive.</summary>
public sealed record Posting(AccountId AccountId, Direction Direction, Money Amount)
{
    /// <summary>
    /// For EF Core only: it cannot pass <see cref="Amount"/> to a constructor, so it creates the
    /// posting empty and then sets every property. Private, so no code can create an empty posting.
    /// </summary>
    private Posting()
        : this(default, default, null!)
    {
    }
}
