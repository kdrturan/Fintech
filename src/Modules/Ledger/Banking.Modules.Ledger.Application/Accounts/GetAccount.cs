using Banking.Modules.Ledger.Application.Abstractions;
using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;
using MediatR;

namespace Banking.Modules.Ledger.Application.Accounts;

public sealed record GetAccountQuery(Guid AccountId) : IRequest<Result<AccountResponse>>;

public sealed class GetAccountHandler(IAccountRepository accounts) : IRequestHandler<GetAccountQuery, Result<AccountResponse>>
{
    public async Task<Result<AccountResponse>> Handle(GetAccountQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = new AccountId(request.AccountId);
        var account = await accounts.FindAsync(id, cancellationToken);

        // The bank's own accounts are not visible through the API: for a caller they do not exist.
        if (account is null || !account.IsCustomerAccount)
        {
            return LedgerErrors.AccountNotFound(id);
        }

        return AccountResponse.From(account);
    }
}
