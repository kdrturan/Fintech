using System.Globalization;
using Banking.Modules.Transfers.Domain;
using Banking.SharedKernel;

namespace Banking.UnitTests.Transfers;

public sealed class TransferTests
{
    private static readonly Guid Ahmet = Guid.CreateVersion7();
    private static readonly Guid Ayse = Guid.CreateVersion7();

    [Fact]
    public void A_new_transfer_is_pending()
    {
        var transfer = PendingTransfer();

        transfer.Status.ShouldBe(TransferStatus.Pending);
        transfer.Id.Value.Version.ShouldBe(7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void The_amount_must_be_positive(int amount)
    {
        var result = Transfer.Create(Ahmet, Ayse, Money.Of(amount, Currency.TRY));

        result.Error.ShouldBe(TransferErrors.AmountMustBePositive);
    }

    [Fact]
    public void Source_and_destination_must_differ()
    {
        var result = Transfer.Create(Ahmet, Ahmet, Money.Of(30m, Currency.TRY));

        result.Error.ShouldBe(TransferErrors.SameAccount);
    }

    [Fact]
    public void Completing_twice_is_harmless()
    {
        var transfer = PendingTransfer();

        transfer.Complete();
        transfer.Complete();

        transfer.Status.ShouldBe(TransferStatus.Completed);
    }

    [Fact]
    public void Failing_twice_keeps_the_first_reason()
    {
        var transfer = PendingTransfer();

        transfer.Fail("ledger.insufficient_funds");
        transfer.Fail("something else");

        transfer.Status.ShouldBe(TransferStatus.Failed);
        transfer.FailureReason.ShouldBe("ledger.insufficient_funds");
    }

    [Fact]
    public void A_completed_transfer_cannot_fail()
    {
        var transfer = PendingTransfer();
        transfer.Complete();

        Should.Throw<InvalidOperationException>(() => transfer.Fail("too late"));
    }

    [Fact]
    public void A_failed_transfer_cannot_complete()
    {
        var transfer = PendingTransfer();
        transfer.Fail("ledger.insufficient_funds");

        Should.Throw<InvalidOperationException>(transfer.Complete);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_failure_needs_a_reason(string reason)
    {
        Should.Throw<ArgumentException>(() => PendingTransfer().Fail(reason));
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("0.01", true)]
    public void The_amount_must_be_greater_than_zero(string amount, bool succeeds)
    {
        var decimalAmount = decimal.Parse(amount, CultureInfo.InvariantCulture);
        var result = Transfer.Create(Ahmet, Ayse, Money.Of(decimalAmount, Currency.TRY));
        result.IsSuccess.ShouldBe(succeeds);
    }

    private static Transfer PendingTransfer() => Transfer.Create(Ahmet, Ayse, Money.Of(30m, Currency.TRY)).Value;
}
