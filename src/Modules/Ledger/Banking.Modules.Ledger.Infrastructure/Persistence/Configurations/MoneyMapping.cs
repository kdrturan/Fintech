using Banking.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Modules.Ledger.Infrastructure.Persistence.Configurations;

internal static class MoneyMapping
{
    /// <summary>Wide enough for any balance, with room for currencies that have up to four decimal places.</summary>
    public const int Precision = 19;

    public const int Scale = 4;

    public static PropertyBuilder<Currency> HasCurrencyConversion(this PropertyBuilder<Currency> property) =>
        property
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code))
            .HasMaxLength(3)
            .IsFixedLength();

    public static ComplexTypePropertyBuilder<Currency> HasCurrencyConversion(this ComplexTypePropertyBuilder<Currency> property) =>
        property
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code))
            .HasMaxLength(3)
            .IsFixedLength();

    public static void MapMoney(this ComplexPropertyBuilder<Money> money, string amountColumn, string currencyColumn)
    {
        money.Property(value => value.Amount).HasColumnName(amountColumn).HasPrecision(Precision, Scale);
        money.Property(value => value.Currency).HasColumnName(currencyColumn).HasCurrencyConversion();
    }
}
