using System.Globalization;
using Banking.SharedKernel;

namespace Banking.UnitTests.SharedKernel;

public sealed class MoneyRoundingTests
{
    [Fact]
    public void Dotnet_rounds_half_to_even_by_default()
    {
        // Not a test of our code: it documents the trap that Money.Round avoids.
        decimal.Round(2.345m, 2).ShouldBe(2.34m);
        decimal.Round(2.355m, 2).ShouldBe(2.36m);
    }

    [Fact]
    public void Round_defaults_to_half_away_from_zero()
    {
        Money.Round(2.345m, Currency.TRY).ShouldBe(Money.Of(2.35m, Currency.TRY));
        Money.Round(-2.345m, Currency.TRY).ShouldBe(Money.Of(-2.35m, Currency.TRY));
    }

    [Fact]
    public void Round_can_use_banker_rounding_when_asked()
    {
        Money.Round(2.345m, Currency.TRY, MidpointRounding.ToEven).ShouldBe(Money.Of(2.34m, Currency.TRY));
    }

    [Fact]
    public void Round_uses_the_currency_precision()
    {
        // 1234.6 is not a midpoint, so this checks only the precision and not the midpoint rule.
        Money.Round(1234.6m, Currency.JPY).ShouldBe(Money.Of(1235m, Currency.JPY));
    }

    [Fact]
    public void Multiply_rounds_the_result()
    {
        var amount = Money.Of(333.33m, Currency.TRY);

        // 333.33 * 0.005 = 1.66665
        amount.Multiply(0.005m).ShouldBe(Money.Of(1.67m, Currency.TRY));
    }

    [Fact]
    public void Allocate_gives_leftover_units_to_the_first_parts()
    {
        var parts = Money.Of(100m, Currency.TRY).Allocate(3);

        parts.ShouldBe([Money.Of(33.34m, Currency.TRY), Money.Of(33.33m, Currency.TRY), Money.Of(33.33m, Currency.TRY)]);
    }

    [Fact]
    public void Allocate_works_for_negative_amounts()
    {
        var parts = Money.Of(-100m, Currency.TRY).Allocate(3);

        parts.ShouldBe([Money.Of(-33.34m, Currency.TRY), Money.Of(-33.33m, Currency.TRY), Money.Of(-33.33m, Currency.TRY)]);
    }

    [Theory]
    [InlineData("100", 3)]
    [InlineData("0.01", 3)]
    [InlineData("1000000", 7)]
    [InlineData("-55.55", 4)]
    [InlineData("10", 1)]
    public void Allocated_parts_always_add_up_to_the_original_amount(string amount, int parts)
    {
        // A string, not a double: -55.55 as a double is -55.549999999999997 and is only correct here by luck.
        var original = Money.Of(decimal.Parse(amount, CultureInfo.InvariantCulture), Currency.TRY);

        var allocated = original.Allocate(parts);

        allocated.Count.ShouldBe(parts);
        allocated.Aggregate(Money.Zero(Currency.TRY), (total, part) => total + part).ShouldBe(original);
        (allocated.Max()!.Amount - allocated.Min()!.Amount).ShouldBeLessThanOrEqualTo(0.01m);
    }

    [Fact]
    public void Allocate_respects_currencies_without_decimals()
    {
        Money.Of(100m, Currency.JPY).Allocate(3)
            .ShouldBe([Money.Of(34m, Currency.JPY), Money.Of(33m, Currency.JPY), Money.Of(33m, Currency.JPY)]);
    }

    [Fact]
    public void Allocate_gives_some_parts_zero_when_there_are_fewer_units_than_parts()
    {
        Money.Of(0.02m, Currency.TRY).Allocate(3)
            .ShouldBe([Money.Of(0.01m, Currency.TRY), Money.Of(0.01m, Currency.TRY), Money.Zero(Currency.TRY)]);
    }

    [Fact]
    public void Allocate_spreads_more_than_one_leftover_unit_one_at_a_time()
    {
        Money.Of(10m, Currency.JPY).Allocate(4)
            .ShouldBe([Money.Of(3m, Currency.JPY), Money.Of(3m, Currency.JPY), Money.Of(2m, Currency.JPY), Money.Of(2m, Currency.JPY)]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Allocate_rejects_a_non_positive_number_of_parts(int parts)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Money.Of(100m, Currency.TRY).Allocate(parts));
    }
}
