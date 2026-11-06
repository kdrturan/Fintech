using Banking.Modules.Ledger.Application.Abstractions;
using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;
using MediatR;

namespace Banking.Modules.Ledger.Application.Accounts;

/// <summary>
/// Cash paid in for a customer, for example at a branch. It stands in for the money that later phases
/// will receive from outside the bank (EFT, card top-ups), so that transfers have something to move.
/// </summary>
public sealed record DepositCommand(Guid AccountId, decimal Amount, string Currency) : IRequest<Result<AccountResponse>>;

public sealed class DepositHandler(IAccountRepository accounts, ILedgerPoster poster) : IRequestHandler<DepositCommand, Result<AccountResponse>>
{
    public async Task<Result<AccountResponse>> Handle(DepositCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var money = Money.TryCreate(request.Amount, request.Currency);
        if (money.IsFailure)
        {
            return money.Error!;
        }

        if (!money.Value.IsPositive)
        {
            return LedgerErrors.AmountMustBePositive;
        }

        var accountId = new AccountId(request.AccountId);
        var account = await accounts.FindAsync(accountId, cancellationToken);
        if (account is null || !account.IsCustomerAccount)
        {
            return LedgerErrors.AccountNotFound(accountId);
        }

        // Debit the bank's cash (an asset grows), credit the customer (the bank owes more).
        var entry = JournalEntry.Create(
            Guid.CreateVersion7(),
            [
                new Posting(BankAccounts.Cash(money.Value.Currency), Direction.Debit, money.Value),
                new Posting(accountId, Direction.Credit, money.Value),
            ]);

        var posted = await poster.PostAsync(entry, cancellationToken);
        if (posted.IsFailure)
        {
            return posted.Error!;
        }

        var updated = await accounts.FindAsync(accountId, cancellationToken);
        return AccountResponse.From(updated!);
    }
}
