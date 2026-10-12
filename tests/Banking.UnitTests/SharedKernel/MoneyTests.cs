using System.Globalization;
using Banking.SharedKernel;

namespace Banking.UnitTests.SharedKernel;

public sealed class MoneyTests
{
    [Theory]
    [InlineData("10.50", "TRY")]
    [InlineData("10.5", "TRY")]
    [InlineData("-3.25", "USD")]
    [InlineData("1000", "JPY")]
    public void Of_accepts_amounts_that_fit_the_currency(string amount, string currencyCode)
    {
        var money = Money.Of(Parse(amount), CurrencyFrom(currencyCode));

        money.Amount.ShouldBe(Parse(amount));
        money.Currency.Code.ShouldBe(currencyCode);
    }

    [Theory]
    [InlineData("10.505", "TRY")]
    [InlineData("0.001", "EUR")]
    [InlineData("100.5", "JPY")]
    public void Of_rejects_more_decimal_places_than_the_currency_allows(string amount, string currencyCode)
    {
        Should.Throw<ArgumentException>(() => Money.Of(Parse(amount), CurrencyFrom(currencyCode)));
    }

    [Fact]
    public void Equal_amounts_in_the_same_currency_are_equal_regardless_of_trailing_zeros()
    {
        Money.Of(10.5m, Currency.TRY).ShouldBe(Money.Of(10.50m, Currency.TRY));
    }

    [Fact]
    public void The_same_amount_in_different_currencies_is_not_equal()
    {
        Money.Of(10m, Currency.TRY).ShouldNotBe(Money.Of(10m, Currency.USD));
    }

    [Fact]
    public void Add_and_subtract_work_within_one_currency()
    {
        var balance = Money.Of(100m, Currency.TRY);

        (balance + Money.Of(0.10m, Currency.TRY)).ShouldBe(Money.Of(100.10m, Currency.TRY));
        (balance - Money.Of(150m, Currency.TRY)).ShouldBe(Money.Of(-50m, Currency.TRY));
    }

    [Fact]
    public void Combining_different_currencies_throws()
    {
        var lira = Money.Of(100m, Currency.TRY);
        var dollars = Money.Of(5m, Currency.USD);

        Should.Throw<InvalidOperationException>(() => lira + dollars);
        Should.Throw<InvalidOperationException>(() => lira > dollars);
    }

    [Fact]
    public void Comparison_uses_the_amount()
    {
        (Money.Of(5m, Currency.EUR) < Money.Of(5.01m, Currency.EUR)).ShouldBeTrue();
        (Money.Of(-1m, Currency.EUR) < Money.Zero(Currency.EUR)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("10.5", "TRY", "10.50 TRY")]
    [InlineData("1234567.8", "USD", "1234567.80 USD")]
    [InlineData("1000", "JPY", "1000 JPY")]
    public void ToString_shows_every_decimal_place_of_the_currency(string amount, string currencyCode, string expected)
    {
        Money.Of(Parse(amount), CurrencyFrom(currencyCode)).ToString().ShouldBe(expected);
    }

    // Amounts are passed as strings because attributes cannot hold decimals.
    // InvariantCulture matters: on a tr-TR machine "10.50" would otherwise parse as 1050.
    private static decimal Parse(string amount) => decimal.Parse(amount, CultureInfo.InvariantCulture);

    private static Currency CurrencyFrom(string code) => code switch
    {
        "TRY" => Currency.TRY,
        "USD" => Currency.USD,
        "EUR" => Currency.EUR,
        "JPY" => Currency.JPY,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown currency in test data."),
    };
}
