namespace Banking.SharedKernel;

public static class MoneyErrors
{
    public static Error UnknownCurrency(string? code) =>
        new("money.unknown_currency", $"'{code}' is not a supported currency.", ErrorType.Validation);

    public static Error TooManyDecimalPlaces(Currency currency) =>
        new("money.too_many_decimal_places", $"{currency.Code} allows at most {currency.MinorUnits} decimal places.", ErrorType.Validation);
}
