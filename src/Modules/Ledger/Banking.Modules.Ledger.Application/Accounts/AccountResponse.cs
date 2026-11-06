using Banking.Modules.Ledger.Domain;

namespace Banking.Modules.Ledger.Application.Accounts;

public sealed record AccountResponse(Guid Id, string Currency, decimal Balance)
{
    internal static AccountResponse From(Account account) =>
        new(account.Id.Value, account.Currency.Code, account.Balance.Amount);
}
