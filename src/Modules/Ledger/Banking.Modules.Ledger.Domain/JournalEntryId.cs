namespace Banking.Modules.Ledger.Domain;

public readonly record struct JournalEntryId(Guid Value)
{
    public static JournalEntryId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
