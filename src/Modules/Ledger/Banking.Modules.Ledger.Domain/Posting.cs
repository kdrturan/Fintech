using Banking.SharedKernel;

namespace Banking.Modules.Ledger.Domain;

/// <summary>One line of a journal entry: which account, which side, how much. The amount is always positive.</summary>
public sealed record Posting(AccountId AccountId, Direction Direction, Money Amount);
