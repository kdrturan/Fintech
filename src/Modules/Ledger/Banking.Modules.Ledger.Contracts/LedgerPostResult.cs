namespace Banking.Modules.Ledger.Contracts;

/// <summary>The outcome of a posting. On failure, <see cref="ErrorCode"/> is a stable code such as <c>ledger.insufficient_funds</c>.</summary>
public sealed record LedgerPostResult(bool Succeeded, string? ErrorCode, string? ErrorMessage, string? ErrorType)
{
    public static LedgerPostResult Success { get; } = new(true, null, null, null);

    public static LedgerPostResult Failure(string code, string message, string type) => new(false, code, message, type);
}
