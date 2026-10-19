using System.Globalization;
using Banking.SharedKernel;

namespace Banking.UnitTests.SharedKernel;

/// <summary>Money built from user input reports problems as validation errors instead of throwing.</summary>
public sealed class MoneyInputTests
{
    [Theory]
    [InlineData("TRY")]
    [InlineData("JPY")]
    public void A_supported_currency_is_found_by_its_code(string code)
    {
        Currency.TryFromCode(code, out var currency).ShouldBeTrue();
        currency!.Code.ShouldBe(code);
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("try")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unknown_currency_code_is_not_found(string? code)
    {
        Currency.TryFromCode(code, out _).ShouldBeFalse();
    }

    [Fact]
    public void Reading_an_unknown_currency_from_our_own_data_is_a_bug()
    {
        Should.Throw<ArgumentException>(() => Currency.FromCode("XYZ"));
    }

    [Theory]
    [InlineData("100", "TRY", "100.00")]
    [InlineData("100.0000", "TRY", "100.00")]
    [InlineData("5.0", "JPY", "5")]
    public void The_amount_always_has_the_currencys_number_of_decimal_places(string amount, string currency, string expected)
    {
        var money = Money.Of(decimal.Parse(amount, CultureInfo.InvariantCulture), Currency.FromCode(currency));

        money.Amount.ToString(CultureInfo.InvariantCulture).ShouldBe(expected);
    }
}
