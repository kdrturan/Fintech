using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;

namespace Banking.UnitTests.Ledger;

public sealed class JournalEntryTests
{
    private static readonly AccountId Ahmet = AccountId.New();
    private static readonly AccountId Ayse = AccountId.New();

    [Fact]
    public void A_balanced_entry_is_created()
    {
        var entry = JournalEntry.Create(Guid.CreateVersion7(), [Debit(Ahmet, 30m), Credit(Ayse, 30m)]);

        entry.Postings.Count.ShouldBe(2);
        entry.Currency.ShouldBe(Currency.TRY);
    }

    [Fact]
    public void An_unbalanced_entry_is_rejected()
    {
        Should.Throw<ArgumentException>(() =>
            JournalEntry.Create(Guid.CreateVersion7(), [Debit(Ahmet, 30m), Credit(Ayse, 29.99m)]));
    }

    [Fact]
    public void An_entry_needs_at_least_two_postings()
    {
        Should.Throw<ArgumentException>(() => JournalEntry.Create(Guid.CreateVersion7(), [Debit(Ahmet, 30m)]));
    }

    [Fact]
    public void All_postings_must_share_one_currency()
    {
        Posting[] postings =
        [
            new(Ahmet, Direction.Debit, Money.Of(30m, Currency.TRY)),
            new(Ayse, Direction.Credit, Money.Of(30m, Currency.USD)),
        ];

        Should.Throw<ArgumentException>(() => JournalEntry.Create(Guid.CreateVersion7(), postings));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Posting_amounts_must_be_positive(int amount)
    {
        Should.Throw<ArgumentException>(() =>
            JournalEntry.Create(Guid.CreateVersion7(), [Debit(Ahmet, amount), Credit(Ayse, amount)]));
    }

    [Fact]
    public void An_entry_needs_an_idempotency_key()
    {
        Should.Throw<ArgumentException>(() => JournalEntry.Create(Guid.Empty, [Debit(Ahmet, 30m), Credit(Ayse, 30m)]));
    }

    [Fact]
    public void Changing_the_original_list_does_not_change_the_entry()
    {
        var postings = new List<Posting> { Debit(Ahmet, 30m), Credit(Ayse, 30m) };
        var entry = JournalEntry.Create(Guid.CreateVersion7(), postings);

        postings.Add(Debit(Ahmet, 1_000_000m));

        entry.Postings.Count.ShouldBe(2);
    }

    [Fact]
    public void Reverse_swaps_every_direction()
    {
        var entry = JournalEntry.Create(Guid.CreateVersion7(), [Debit(Ahmet, 30m), Credit(Ayse, 30m)]);

        var reversal = entry.Reverse(Guid.CreateVersion7());

        reversal.Postings.ShouldBe([Credit(Ahmet, 30m), Debit(Ayse, 30m)]);
        reversal.IdempotencyKey.ShouldNotBe(entry.IdempotencyKey);
    }

    private static Posting Debit(AccountId account, decimal amount) => new(account, Direction.Debit, Money.Of(amount, Currency.TRY));

    private static Posting Credit(AccountId account, decimal amount) => new(account, Direction.Credit, Money.Of(amount, Currency.TRY));
}
