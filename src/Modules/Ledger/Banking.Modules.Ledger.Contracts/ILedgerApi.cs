namespace Banking.Modules.Ledger.Contracts;

/// <summary>
/// What other modules may ask of the Ledger. Built from primitive types only, so a caller never depends on
/// Ledger types and this contract could later be served over the network. See docs/adr/0003.
/// </summary>
public interface ILedgerApi
{
    /// <summary>
    /// Moves money from one customer account to another. Posting the same <paramref name="idempotencyKey"/> again
    /// returns the original result instead of moving the money twice.
    /// </summary>
    Task<LedgerPostResult> TransferAsync(
        Guid idempotencyKey,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken);
}
