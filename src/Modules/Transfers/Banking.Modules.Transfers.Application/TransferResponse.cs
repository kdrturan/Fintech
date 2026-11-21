using System.Text.Json.Serialization;
using Banking.Modules.Transfers.Domain;

namespace Banking.Modules.Transfers.Application;

public sealed record TransferResponse(
    Guid Id,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency,
    string Status,
    string? FailureReason)
{
    [JsonIgnore]
    public bool HasFailed => Status == nameof(TransferStatus.Failed);

    internal static TransferResponse From(Transfer transfer) => new(
        transfer.Id.Value,
        transfer.SourceAccountId,
        transfer.DestinationAccountId,
        transfer.Amount.Amount,
        transfer.Amount.Currency.Code,
        transfer.Status.ToString(),
        transfer.FailureReason);
}
