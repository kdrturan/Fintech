using Banking.SharedKernel;

namespace Banking.Modules.Ledger.Domain;

/// <summary>
/// The bank's own accounts that every flow needs. Their ids are fixed so the ledger can find them
/// without a lookup; a migration creates them. A new currency needs a new cash account here and in that migration.
/// </summary>
public static class BankAccounts
{
    /// <summary>The cash and clearing account a customer deposit comes from (an asset).</summary>
    public static AccountId Cash(Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        return currency.Code switch
        {
            "TRY" => new(new Guid("00000000-0000-7000-8000-000000000101")),
            "USD" => new(new Guid("00000000-0000-7000-8000-000000000102")),
            "EUR" => new(new Guid("00000000-0000-7000-8000-000000000103")),
            "JPY" => new(new Guid("00000000-0000-7000-8000-000000000104")),
            _ => throw new ArgumentException($"There is no cash account for {currency.Code}.", nameof(currency)),
        };
    }
}
