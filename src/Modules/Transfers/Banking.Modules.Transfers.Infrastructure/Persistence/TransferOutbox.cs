using Banking.Messaging.Outbox;
using Banking.Modules.Transfers.Contracts;
using Banking.Modules.Transfers.Domain;

namespace Banking.Modules.Transfers.Infrastructure.Persistence;

/// <summary>Turns what a transfer recorded (Domain) into what other modules may read (Contracts).</summary>
internal static class TransferOutbox
{
    public static IEnumerable<OutboxMessage> MessagesFor(Transfer transfer) =>
        transfer.Events.Select(happened => happened switch
        {
            TransferCompletedEvent => OutboxMessage.Create(
                TransferCompleted.EventType,
                new TransferCompleted(
                    transfer.Id.Value,
                    transfer.RequestedBy,
                    transfer.SourceAccountId,
                    transfer.DestinationAccountId,
                    transfer.Amount.Amount,
                    transfer.Amount.Currency.Code)),

            TransferFailedEvent failed => OutboxMessage.Create(
                TransferFailed.EventType,
                new TransferFailed(
                    transfer.Id.Value,
                    transfer.RequestedBy,
                    transfer.SourceAccountId,
                    transfer.DestinationAccountId,
                    transfer.Amount.Amount,
                    transfer.Amount.Currency.Code,
                    failed.Reason)),

            _ => throw new InvalidOperationException($"No integration event for {happened.GetType().Name}."),
        });
}
