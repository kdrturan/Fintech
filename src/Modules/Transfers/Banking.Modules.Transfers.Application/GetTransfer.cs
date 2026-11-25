using Banking.Modules.Transfers.Application.Abstractions;
using Banking.Modules.Transfers.Domain;
using Banking.SharedKernel;
using MediatR;

namespace Banking.Modules.Transfers.Application;

public sealed record GetTransferQuery(Guid UserId, Guid TransferId) : IRequest<Result<TransferResponse>>;

public sealed class GetTransferHandler(ITransferRepository transfers) : IRequestHandler<GetTransferQuery, Result<TransferResponse>>
{
    public async Task<Result<TransferResponse>> Handle(GetTransferQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = new TransferId(request.TransferId);
        var transfer = await transfers.FindAsync(id, cancellationToken);

        // Someone else's transfer gets the same answer as one that does not exist.
        return transfer is null || transfer.RequestedBy != request.UserId
            ? TransferErrors.NotFound(id)
            : TransferResponse.From(transfer);
    }
}
