using System.Diagnostics.CodeAnalysis;

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

    /// <summary>Every supported currency. Adding one here is the only way to support a new currency.</summary>
    public static IReadOnlyList<Currency> All { get; } = [TRY, USD, EUR, JPY];

    private Currency(string code, int minorUnits)
    {
        Code = code;
        MinorUnits = minorUnits;
    }

    public string Code { get; }

    public int MinorUnits { get; }

    /// <summary>Finds a supported currency by its ISO 4217 code, for example from an HTTP request.</summary>
    public static bool TryFromCode(string? code, [NotNullWhen(true)] out Currency? currency)
    {
        currency = All.FirstOrDefault(candidate => string.Equals(candidate.Code, code, StringComparison.Ordinal));
        return currency is not null;
    }

    /// <summary>
    /// Finds a supported currency by code where an unknown code can only be a bug, such as data read from our own database.
    /// </summary>
    public static Currency FromCode(string code) =>
        TryFromCode(code, out var currency)
            ? currency
            : throw new ArgumentException($"'{code}' is not a supported currency.", nameof(code));

    public override string ToString() => Code;
}
