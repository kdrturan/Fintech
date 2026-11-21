using Banking.Modules.Transfers.Application.Abstractions;
using Banking.Modules.Transfers.Domain;
using Banking.SharedKernel;
using MediatR;

namespace Banking.Modules.Transfers.Application;

public sealed record GetTransferQuery(Guid TransferId) : IRequest<Result<TransferResponse>>;

public sealed class GetTransferHandler(ITransferRepository transfers) : IRequestHandler<GetTransferQuery, Result<TransferResponse>>
{
    public async Task<Result<TransferResponse>> Handle(GetTransferQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = new TransferId(request.TransferId);
        var transfer = await transfers.FindAsync(id, cancellationToken);

        return transfer is null ? TransferErrors.NotFound(id) : TransferResponse.From(transfer);
    }
}
