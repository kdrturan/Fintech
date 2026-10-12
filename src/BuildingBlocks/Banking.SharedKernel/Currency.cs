namespace Banking.SharedKernel;

/// <summary>
/// An ISO 4217 currency and the number of digits its smallest unit allows (TRY 2 for kuruş, JPY 0).
/// Only the currencies the bank supports exist; there is no way to create another one.
/// </summary>
public sealed record Currency
{
    public static readonly Currency TRY = new("TRY", 2);
    public static readonly Currency USD = new("USD", 2);
    public static readonly Currency EUR = new("EUR", 2);
    public static readonly Currency JPY = new("JPY", 0);

    private Currency(string code, int minorUnits)
    {
        Code = code;
        MinorUnits = minorUnits;
    }

    public string Code { get; }

    public int MinorUnits { get; }

    public override string ToString() => Code;
}
