namespace Banking.Modules.Transfers.Domain;

public enum TransferStatus
{
    /// <summary>Recorded, but the Ledger has not confirmed the money movement yet.</summary>
    Pending,

    /// <summary>The Ledger posted the entry. Final.</summary>
    Completed,

    /// <summary>The Ledger rejected the entry, for example for insufficient funds. Final.</summary>
    Failed,
}
