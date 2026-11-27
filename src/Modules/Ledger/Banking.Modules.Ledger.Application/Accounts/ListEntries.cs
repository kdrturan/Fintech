using Banking.Modules.Ledger.Application.Abstractions;
using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;
using MediatR;

namespace Banking.Modules.Ledger.Application.Accounts;

/// <param name="UserId">Who asks.</param>
/// <param name="AccountId">The account whose entries they want.</param>
/// <param name="Limit">At most this many entries, from 1 to <see cref="MaxLimit"/>.</param>
/// <param name="After">The previous page's <see cref="EntryListResponse.NextCursor"/>, or null for the first page.</param>
public sealed record ListEntriesQuery(Guid UserId, Guid AccountId, int Limit, string? After) : IRequest<Result<EntryListResponse>>
{
    public const int DefaultLimit = 20;

    public const int MaxLimit = 100;
}

/// <param name="Items">Newest first.</param>
/// <param name="NextCursor">What to send as <c>after</c> for the next page; null when this is the last one.</param>
public sealed record EntryListResponse(IReadOnlyList<EntryResponse> Items, string? NextCursor);

/// <param name="Id">The journal entry. Both sides of a transfer see the same one.</param>
/// <param name="OperationId">What caused it: for a transfer, the transfer's id.</param>
/// <param name="Amount">Positive when money came in, negative when it went out.</param>
/// <param name="Currency">The account's currency.</param>
/// <param name="CreatedAt">When the posting started. The order of the list, not this, says which change came first.</param>
public sealed record EntryResponse(Guid Id, Guid OperationId, decimal Amount, string Currency, DateTimeOffset CreatedAt);

public sealed class ListEntriesHandler(IAccountRepository accounts, IAccountEntryReader entries)
    : IRequestHandler<ListEntriesQuery, Result<EntryListResponse>>
{
    public async Task<Result<EntryListResponse>> Handle(ListEntriesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        long? before = null;
        if (request.After is not null)
        {
            if (!EntryCursor.TryDecode(request.After, out var position))
            {
                return EntryCursor.Invalid;
            }

            before = position;
        }

        // Checked on every page. A cursor is a position, not a permission.
        var id = new AccountId(request.AccountId);
        var account = await accounts.FindAsync(id, cancellationToken);
        if (account is null || !account.IsOwnedBy(request.UserId))
        {
            return LedgerErrors.AccountNotFound(id);
        }

        // One more than asked for: it is not returned, it only tells whether there is a next page.
        var found = await entries.ListAsync(id, before, request.Limit + 1, cancellationToken);
        var page = found.Take(request.Limit).ToList();
        var nextCursor = found.Count > request.Limit ? EntryCursor.Encode(page[^1].Position) : null;

        return new EntryListResponse([.. page.Select(entry => ToResponse(account, entry))], nextCursor);
    }

    private static EntryResponse ToResponse(Account account, AccountEntry entry)
    {
        // Debit and credit are the bank's words. The account knows which of them adds money to it.
        var change = account.EffectOf(entry.Direction, entry.Amount);
        return new EntryResponse(entry.EntryId.Value, entry.OperationId, change.Amount, change.Currency.Code, entry.CreatedAt);
    }
}
