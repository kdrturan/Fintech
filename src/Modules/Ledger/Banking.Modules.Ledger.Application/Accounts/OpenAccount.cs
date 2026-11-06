using Banking.Modules.Ledger.Application.Abstractions;
using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;
using MediatR;

namespace Banking.Modules.Ledger.Application.Accounts;

public sealed record OpenAccountCommand(string Currency) : IRequest<Result<AccountResponse>>;

public sealed class OpenAccountHandler(IAccountRepository accounts) : IRequestHandler<OpenAccountCommand, Result<AccountResponse>>
{
    public async Task<Result<AccountResponse>> Handle(OpenAccountCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Currency.TryFromCode(request.Currency, out var currency))
        {
            return MoneyErrors.UnknownCurrency(request.Currency);
        }

        var account = Account.OpenCustomerAccount(currency);
        await accounts.AddAsync(account, cancellationToken);

        return AccountResponse.From(account);
    }
}
