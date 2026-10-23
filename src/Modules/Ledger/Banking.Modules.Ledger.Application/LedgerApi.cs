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
        foreach (var id in ids)
        {
            // Account types never change, so checking them before the posting transaction is safe.
            var account = await accounts.FindAsync(id, cancellationToken);
            if (account is null || !account.IsCustomerAccount)
            {
                return Failure(LedgerErrors.AccountNotFound(id));
            }
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
