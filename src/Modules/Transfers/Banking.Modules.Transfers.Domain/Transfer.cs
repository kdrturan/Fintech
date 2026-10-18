using Banking.SharedKernel;

namespace Banking.Modules.Transfers.Domain;

/// <summary>
/// A request to move money between two accounts. Transfers never change balances themselves;
/// they ask the Ledger to post, using <see cref="Id"/> as the idempotency key. See docs/adr/0003.
/// </summary>
public sealed class Transfer
{
    private Transfer(TransferId id, Guid sourceAccountId, Guid destinationAccountId, Money amount)
    {
        Id = id;
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
        Amount = amount;
        Status = TransferStatus.Pending;
    }

    public TransferId Id { get; }

    /// <summary>
    /// Accounts belong to the Ledger module. Transfers may not reference Ledger.Domain,
    /// so it stores the plain id, the same primitive Ledger.Contracts will expose.
    /// </summary>
    public Guid SourceAccountId { get; }

    public Guid DestinationAccountId { get; }

    public Money Amount { get; }

    public TransferStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    public static Result<Transfer> Create(Guid sourceAccountId, Guid destinationAccountId, Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!amount.IsPositive)
        {
            return TransferErrors.AmountMustBePositive;
        }

        if (sourceAccountId == destinationAccountId)
        {
            return TransferErrors.SameAccount;
        }

        return new Transfer(TransferId.New(), sourceAccountId, destinationAccountId, amount);
    }

    /// <summary>
    /// Marks the transfer as completed after the Ledger posted it. Calling it again is harmless,
    /// so the recovery job can safely retry; completing a failed transfer is a bug and throws.
    /// </summary>
    public void Complete()
    {
        switch (Status)
        {
            case TransferStatus.Completed:
                return;
            case TransferStatus.Failed:
                throw new InvalidOperationException($"Transfer {Id} has failed and cannot be completed.");
            default:
                Status = TransferStatus.Completed;
                break;
        }
    }

    /// <summary>Marks the transfer as failed. Calling it again is harmless; failing a completed transfer is a bug and throws.</summary>
    public void Fail(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        switch (Status)
        {
            case TransferStatus.Failed:
                return;
            case TransferStatus.Completed:
                throw new InvalidOperationException($"Transfer {Id} has completed and cannot fail.");
            default:
                Status = TransferStatus.Failed;
                FailureReason = reason;
                break;
        }
    }
}
