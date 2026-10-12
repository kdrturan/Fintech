using System.Globalization;

namespace Banking.SharedKernel;

/// <summary>
/// An amount in a specific currency. The amount never has more decimal places than the currency allows,
/// so a Money value is always something that can actually be paid out. See docs/adr/0006.
/// </summary>
public sealed record Money : IComparable<Money>
{
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public bool IsZero => Amount == 0m;

    public bool IsPositive => Amount > 0m;

    public bool IsNegative => Amount < 0m;

    /// <summary>Creates an exact amount. Throws if it has more decimal places than the currency allows.</summary>
    public static Money Of(decimal amount, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (decimal.Round(amount, currency.MinorUnits) != amount)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"{currency.Code} allows at most {currency.MinorUnits} decimal places, but got {amount}. Use {nameof(Round)} when rounding is intended."),
                nameof(amount));
        }

        return new Money(amount, currency);
    }

    public static Money Zero(Currency currency) => Of(0m, currency);

    /// <summary>
    /// Rounds a calculated amount (interest, fees, exchange) to the currency's smallest unit.
    /// The default is half away from zero (2.5 becomes 3), not .NET's default banker's rounding.
    /// </summary>
    public static Money Round(decimal amount, Currency currency, MidpointRounding mode = MidpointRounding.AwayFromZero)
    {
        ArgumentNullException.ThrowIfNull(currency);
        return new Money(decimal.Round(amount, currency.MinorUnits, mode), currency);
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Negate() => new(-Amount, Currency);

    /// <summary>Multiplies by a factor such as a fee rate, rounding the result to the currency's smallest unit.</summary>
    public Money Multiply(decimal factor, MidpointRounding mode = MidpointRounding.AwayFromZero) =>
        Round(Amount * factor, Currency, mode);

    /// <summary>
    /// Splits the amount into <paramref name="parts"/> pieces that differ by at most one smallest unit
    /// and always add up to the original amount. 100.00 TRY / 3 = 33.34 + 33.33 + 33.33.
    /// </summary>
    public IReadOnlyList<Money> Allocate(int parts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parts);

        var smallestUnit = SmallestUnit(Currency);
        var totalUnits = Amount / smallestUnit;
        var unitsPerPart = decimal.Truncate(totalUnits / parts);
        var leftoverUnits = totalUnits - (unitsPerPart * parts);
        var step = Math.Sign(leftoverUnits);

        var result = new List<Money>(parts);
        for (var i = 0; i < parts; i++)
        {
            var units = i < Math.Abs(leftoverUnits) ? unitsPerPart + step : unitsPerPart;
            result.Add(new Money(units * smallestUnit, Currency));
        }

        return result;
    }

    public int CompareTo(Money? other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount.ToString("F" + Currency.MinorUnits, CultureInfo.InvariantCulture)} {Currency.Code}");

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator -(Money money) => money.Negate();

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    private void EnsureSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // Mixing currencies is a programming error, not a business outcome: no valid flow adds TRY to USD.
        if (other.Currency != Currency)
        {
            throw new InvalidOperationException($"Cannot combine {Currency.Code} with {other.Currency.Code}.");
        }
    }

    private static decimal SmallestUnit(Currency currency)
    {
        var unit = 1m;
        for (var i = 0; i < currency.MinorUnits; i++)
        {
            unit /= 10;
        }

        return unit;
    }
}
