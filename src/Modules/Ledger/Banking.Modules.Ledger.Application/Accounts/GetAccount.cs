using Banking.Modules.Ledger.Application.Abstractions;
using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;
using MediatR;

namespace Banking.Modules.Ledger.Application.Accounts;

public sealed record GetAccountQuery(Guid UserId, Guid AccountId) : IRequest<Result<AccountResponse>>;

public sealed class GetAccountHandler(IAccountRepository accounts) : IRequestHandler<GetAccountQuery, Result<AccountResponse>>
{
    public async Task<Result<AccountResponse>> Handle(GetAccountQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = new AccountId(request.AccountId);
        var account = await accounts.FindAsync(id, cancellationToken);

        // Someone else's account gets the same answer as one that does not exist. A 403 would confirm that it does.
        // The bank's own accounts have no owner, so nobody sees them.
        if (account is null || !account.IsOwnedBy(request.UserId))
        {
            return LedgerErrors.AccountNotFound(id);
        }

        return AccountResponse.From(account);
    }
}
