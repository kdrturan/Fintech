namespace Banking.Modules.Transfers.Contracts;

/// <summary>
/// Published when the Ledger has moved a transfer's money. A contract: other modules, and later other services, rely on
/// it. Fields are never removed or given a new meaning; a breaking change is a new type with a new version.
/// </summary>
/// <param name="TransferId">The transfer, as <c>GET /transfers/{id}</c> knows it.</param>
/// <param name="RequestedBy">The user who asked for the transfer.</param>
/// <param name="SourceAccountId">The account the money comes from.</param>
/// <param name="DestinationAccountId">The account the money goes to.</param>
/// <param name="Amount">Always positive.</param>
/// <param name="Currency">ISO 4217 code, such as <c>TRY</c>.</param>
public sealed record TransferCompleted(
    Guid TransferId,
    Guid RequestedBy,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency)
{
    public const string EventType = "transfers.transfer_completed.v1";
}
