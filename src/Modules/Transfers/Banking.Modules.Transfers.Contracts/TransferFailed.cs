namespace Banking.Modules.Transfers.Contracts;

/// <summary>
/// Published when the Ledger has rejected a transfer: no money moved. The Ledger leaves no trace of a rejection, so
/// this is the only event that tells anyone about it. See <see cref="TransferCompleted"/> for the rules of a contract.
/// </summary>
/// <param name="TransferId">The transfer, as <c>GET /transfers/{id}</c> knows it.</param>
/// <param name="RequestedBy">The user who asked for the transfer.</param>
/// <param name="SourceAccountId">The account the money comes from.</param>
/// <param name="DestinationAccountId">The account the money goes to.</param>
/// <param name="Amount">Always positive.</param>
/// <param name="Currency">ISO 4217 code, such as <c>TRY</c>.</param>
/// <param name="Reason">The Ledger's error code, such as <c>ledger.insufficient_funds</c>.</param>
public sealed record TransferFailed(
    Guid TransferId,
    Guid RequestedBy,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency,
    string Reason)
{
    public const string EventType = "transfers.transfer_failed.v1";
}
