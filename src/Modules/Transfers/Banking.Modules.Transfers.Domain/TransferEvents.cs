namespace Banking.Modules.Transfers.Domain;

/// <summary>
/// Something that happened to a transfer, recorded by the transfer itself. Internal to the module: persistence turns
/// these into integration events (Transfers.Contracts) in the same transaction as the change.
/// </summary>
public interface ITransferEvent
{
    TransferId TransferId { get; }
}

/// <summary>The Ledger posted the transfer: the money moved.</summary>
public sealed record TransferCompletedEvent(TransferId TransferId) : ITransferEvent;

/// <summary>The Ledger rejected the transfer: no money moved.</summary>
public sealed record TransferFailedEvent(TransferId TransferId, string Reason) : ITransferEvent;
