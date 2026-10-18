using Banking.SharedKernel;

namespace Banking.Modules.Transfers.Domain;

public static class TransferErrors
{
    public static Error AmountMustBePositive { get; } =
        new("transfers.amount_must_be_positive", "The transfer amount must be greater than zero.");

    public static Error SameAccount { get; } =
        new("transfers.same_account", "The source and destination accounts must be different.");
}
