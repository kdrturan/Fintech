using Banking.Modules.Ledger.Contracts;
using Banking.Modules.Transfers.Application.Abstractions;
using Banking.Modules.Transfers.Domain;
using Banking.SharedKernel;
using MediatR;

namespace Banking.Modules.Transfers.Application;

/// <summary>
/// Moves money between two customer accounts, synchronously: the response carries the final status.
/// A request that fails the transfer's own rules creates nothing; a transfer the Ledger rejects is kept as Failed.
/// </summary>
public sealed record CreateTransferCommand(Guid SourceAccountId, Guid DestinationAccountId, decimal Amount, string Currency)
    : IRequest<Result<TransferResponse>>;

public sealed class CreateTransferHandler(ITransferRepository transfers, ILedgerApi ledger)
    : IRequestHandler<CreateTransferCommand, Result<TransferResponse>>
{
    public async Task<Result<TransferResponse>> Handle(CreateTransferCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var amount = Money.TryCreate(request.Amount, request.Currency);
        if (amount.IsFailure)
        {
            return amount.Error!;
        }

        var created = Transfer.Create(request.SourceAccountId, request.DestinationAccountId, amount.Value);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        // 1. Record the intent first. If the process dies after this line, the recovery job finishes the transfer.
        var transfer = created.Value;
        await transfers.AddAsync(transfer, cancellationToken);

        // 2. Move the money. Its own transaction, in the Ledger.
        var posted = await TransferSettlement.PostAsync(ledger, transfer, cancellationToken);

        // 3. Record the outcome. A conflict means the recovery job got there first; its outcome stands.
        TransferSettlement.Record(transfer, posted);
        if (!await transfers.TrySaveAsync(transfer, cancellationToken))
        {
            transfer = await transfers.FindAsync(transfer.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Transfer {transfer.Id} disappeared.");
        }

        return TransferResponse.From(transfer);
    }
}
