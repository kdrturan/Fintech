using Banking.SharedKernel;

namespace Banking.Modules.Ledger.Domain;

public static class LedgerErrors
{
    public static Error AccountNotFound(AccountId accountId) =>
        new("ledger.account_not_found", $"Account {accountId} does not exist.", ErrorType.NotFound);

    public static Error AmountMustBePositive { get; } =
        new("ledger.amount_must_be_positive", "The amount must be greater than zero.");

    public static Error InsufficientFunds(AccountId accountId) =>
        new("ledger.insufficient_funds", $"Account {accountId} does not have enough funds.");

    public static Error CurrencyMismatch(AccountId accountId, Currency accountCurrency, Currency postingCurrency) =>
        new("ledger.currency_mismatch", $"Account {accountId} holds {accountCurrency.Code} and cannot be posted in {postingCurrency.Code}.");
}
