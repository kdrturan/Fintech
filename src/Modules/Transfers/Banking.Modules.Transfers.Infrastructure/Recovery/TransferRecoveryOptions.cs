namespace Banking.Modules.Transfers.Infrastructure.Recovery;

public sealed class TransferRecoveryOptions
{
    public const string SectionName = "TransferRecovery";

    /// <summary>Tests switch the background loop off and run <see cref="StuckTransferRecovery"/> themselves.</summary>
    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A request finishes a transfer within milliseconds. Only a transfer that is still pending after this long
    /// is considered stuck; picking up younger ones would race with the request that is still working on them.
    /// </summary>
    public TimeSpan StuckAfter { get; set; } = TimeSpan.FromSeconds(30);

    public int BatchSize { get; set; } = 20;
}
