using Banking.SharedKernel;

namespace Banking.Modules.Ledger.Domain;

public static class LedgerErrors
{
    public static Error InsufficientFunds(AccountId accountId) =>
        new("ledger.insufficient_funds", $"Account {accountId} does not have enough funds.");

    public static Error CurrencyMismatch(AccountId accountId, Currency accountCurrency, Currency postingCurrency) =>
        new("ledger.currency_mismatch", $"Account {accountId} holds {accountCurrency.Code} and cannot be posted in {postingCurrency.Code}.");
}
