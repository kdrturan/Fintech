using Banking.Modules.Ledger.Application.Abstractions;
using MediatR;

namespace Banking.Modules.Ledger.Application.Accounts;

public sealed record ListAccountsQuery(Guid UserId) : IRequest<AccountListResponse>;

/// <summary>An object around the list, not a bare array, so that a cursor can be added later without breaking clients.</summary>
public sealed record AccountListResponse(IReadOnlyList<AccountResponse> Items);

public sealed class ListAccountsHandler(IAccountRepository accounts) : IRequestHandler<ListAccountsQuery, AccountListResponse>
{
    public async Task<AccountListResponse> Handle(ListAccountsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Nothing to check afterwards: the owner is part of the question, so only their own accounts can come back.
        var owned = await accounts.ListByOwnerAsync(request.UserId, cancellationToken);
        return new AccountListResponse([.. owned.Select(AccountResponse.From)]);
    }
}
