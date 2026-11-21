using Banking.Modules.Ledger.Contracts;
using Banking.Modules.Transfers.Domain;

namespace Banking.Modules.Transfers.Application;

/// <summary>
/// Steps 2 and 3 of a transfer (docs/adr/0003): ask the Ledger to move the money, then record the outcome.
/// Both the request and the recovery job use it. The Ledger call is idempotent on the transfer id,
/// so running it again for a transfer whose money already moved returns the original success.
/// </summary>
public static class TransferSettlement
{
    public static Task<LedgerPostResult> PostAsync(ILedgerApi ledger, Transfer transfer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(transfer);

        return ledger.TransferAsync(
            transfer.Id.Value,
            transfer.SourceAccountId,
            transfer.DestinationAccountId,
            transfer.Amount.Amount,
            transfer.Amount.Currency.Code,
            cancellationToken);
    }

    public static void Record(Transfer transfer, LedgerPostResult result)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        ArgumentNullException.ThrowIfNull(result);

        if (result.Succeeded)
        {
            transfer.Complete();
        }
        else
        {
            transfer.Fail(result.ErrorCode!);
        }
    }
}
