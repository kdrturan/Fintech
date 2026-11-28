using Banking.SharedKernel;

namespace Banking.Modules.Transfers.Domain;

/// <summary>
/// A request to move money between two accounts. Transfers never change balances themselves;
/// they ask the Ledger to post, using <see cref="Id"/> as the idempotency key. See docs/adr/0003.
/// </summary>
public sealed class Transfer
{
    private readonly List<ITransferEvent> _events = [];

    private Transfer(TransferId id, Guid requestedBy, Guid sourceAccountId, Guid destinationAccountId, Money amount)
    {
        Id = id;
        RequestedBy = requestedBy;
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
        Amount = amount;
        Status = TransferStatus.Pending;
    }

    /// <summary>
    /// For EF Core only: it cannot pass <see cref="Amount"/> to a constructor, so it creates the transfer
    /// empty and then sets every property. Private, so no code can create a transfer that skipped the rules.
    /// </summary>
    private Transfer()
    {
        Amount = null!;
    }

    public TransferId Id { get; }

    /// <summary>
    /// The user who asked for the transfer. Kept because the recovery job posts it later, when there is no request and
    /// no token to take the user from, and the Ledger must still check that the source account is theirs.
    /// </summary>
    public Guid RequestedBy { get; }

    /// <summary>
    /// Accounts belong to the Ledger module. Transfers may not reference Ledger.Domain,
    /// so it stores the plain id, the same primitive Ledger.Contracts will expose.
    /// </summary>
    public Guid SourceAccountId { get; }

    public Guid DestinationAccountId { get; }

    public Money Amount { get; }

    public TransferStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    /// <summary>
    /// What happened to this transfer since it was last saved. Only real changes are recorded: completing a completed
    /// transfer again records nothing, so a retry cannot announce the same outcome twice.
    /// </summary>
    public IReadOnlyList<ITransferEvent> Events => _events;

    /// <summary>Called by persistence once the events are stored.</summary>
    public void ClearEvents() => _events.Clear();

    public static Result<Transfer> Create(Guid requestedBy, Guid sourceAccountId, Guid destinationAccountId, Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (requestedBy == Guid.Empty)
        {
            throw new ArgumentException("A transfer needs the user who requested it.", nameof(requestedBy));
        }

        if (!amount.IsPositive)
        {
            return TransferErrors.AmountMustBePositive;
        }

        if (sourceAccountId == destinationAccountId)
        {
            return TransferErrors.SameAccount;
        }

        return new Transfer(TransferId.New(), requestedBy, sourceAccountId, destinationAccountId, amount);
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
                _events.Add(new TransferCompletedEvent(Id));
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
                _events.Add(new TransferFailedEvent(Id, reason));
                break;
        }
    }
}
