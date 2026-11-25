using Banking.Modules.Ledger.Application.Abstractions;
using Banking.Modules.Ledger.Contracts;
using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;

namespace Banking.Modules.Ledger.Application;

/// <summary>The Ledger's side of <see cref="ILedgerApi"/>: turns a request from another module into a journal entry.</summary>
public sealed class LedgerApi(IAccountRepository accounts, ILedgerPoster poster) : ILedgerApi
{
    public async Task<LedgerPostResult> TransferAsync(
        Guid idempotencyKey,
        Guid requestedBy,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        var money = Money.TryCreate(amount, currency);
        if (money.IsFailure)
        {
            return Failure(money.Error!);
        }

        AccountId[] ids = [new(sourceAccountId), new(destinationAccountId)];

        // Owners and account types never change, so checking them before the posting transaction is safe. The source must
        // be the requester's own; someone else's account gets the same answer as one that does not exist. The check is
        // here, where the money moves, so no caller can skip it: not the API, and not the recovery job.
        var source = await accounts.FindAsync(ids[0], cancellationToken);
        if (source is null || !source.IsOwnedBy(requestedBy))
        {
            return Failure(LedgerErrors.AccountNotFound(ids[0]));
        }

        // Money may be sent to anyone's account, but not to the bank's own.
        var destination = await accounts.FindAsync(ids[1], cancellationToken);
        if (destination is null || !destination.IsCustomerAccount)
        {
            return Failure(LedgerErrors.AccountNotFound(ids[1]));
        }

        var entry = JournalEntry.Create(
            idempotencyKey,
            [
                new Posting(ids[0], Direction.Debit, money.Value),
                new Posting(ids[1], Direction.Credit, money.Value),
            ]);

        var posted = await poster.PostAsync(entry, cancellationToken);
        return posted.IsSuccess ? LedgerPostResult.Success : Failure(posted.Error!);
    }

    private static LedgerPostResult Failure(Error error) =>
        LedgerPostResult.Failure(error.Code, error.Message, error.Type.ToString());
}
