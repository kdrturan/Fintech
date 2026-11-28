using Banking.Modules.Transfers.Domain;
using Banking.SharedKernel;

namespace Banking.UnitTests.Transfers;

/// <summary>A transfer records what happened to it, once per real change. Persistence turns the records into outbox rows.</summary>
public sealed class TransferEventTests
{
    [Fact]
    public void A_new_transfer_has_nothing_to_tell()
    {
        PendingTransfer().Events.ShouldBeEmpty();
    }

    [Fact]
    public void Completing_records_that_the_transfer_completed()
    {
        var transfer = PendingTransfer();

        transfer.Complete();

        transfer.Events.ShouldBe([new TransferCompletedEvent(transfer.Id)]);
    }

    [Fact]
    public void Failing_records_the_reason()
    {
        var transfer = PendingTransfer();

        transfer.Fail("ledger.insufficient_funds");

        transfer.Events.ShouldBe([new TransferFailedEvent(transfer.Id, "ledger.insufficient_funds")]);
    }

    [Fact]
    public void Completing_again_records_nothing_more()
    {
        var transfer = PendingTransfer();
        transfer.Complete();
        transfer.ClearEvents();

        // The recovery job may finish a transfer whose money already moved; that must not announce it a second time.
        transfer.Complete();

        transfer.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Failing_again_records_nothing_more()
    {
        var transfer = PendingTransfer();
        transfer.Fail("ledger.insufficient_funds");

        transfer.Fail("something else");

        transfer.Events.ShouldHaveSingleItem();
    }

    [Fact]
    public void A_change_that_is_refused_records_nothing()
    {
        var transfer = PendingTransfer();
        transfer.Fail("ledger.insufficient_funds");
        transfer.ClearEvents();

        Should.Throw<InvalidOperationException>(transfer.Complete);

        transfer.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Clearing_forgets_what_was_recorded()
    {
        var transfer = PendingTransfer();
        transfer.Complete();

        transfer.ClearEvents();

        transfer.Events.ShouldBeEmpty();
    }

    private static Transfer PendingTransfer() =>
        Transfer.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Money.Of(30m, Currency.TRY)).Value;
}
