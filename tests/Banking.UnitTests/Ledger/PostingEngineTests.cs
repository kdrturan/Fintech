using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;

namespace Banking.UnitTests.Ledger;

public sealed class PostingEngineTests
{
    private readonly Account _cash = Account.OpenInternalAccount(AccountType.Asset, Currency.TRY);
    private readonly Account _feeIncome = Account.OpenInternalAccount(AccountType.Income, Currency.TRY);
    private readonly Account _ahmet = Account.OpenCustomerAccount(Currency.TRY);
    private readonly Account _ayse = Account.OpenCustomerAccount(Currency.TRY);

    [Fact]
    public void A_cash_deposit_increases_both_the_bank_cash_and_the_customer_balance()
    {
        var result = Post(Debit(_cash, 100m), Credit(_ahmet, 100m));

        result.IsSuccess.ShouldBeTrue();
        _cash.Balance.ShouldBe(Lira(100m));
        _ahmet.Balance.ShouldBe(Lira(100m));
    }

    [Fact]
    public void A_transfer_moves_money_between_customers()
    {
        Post(Debit(_cash, 100m), Credit(_ahmet, 100m));

        var result = Post(Debit(_ahmet, 30m), Credit(_ayse, 30m));

        result.IsSuccess.ShouldBeTrue();
        _ahmet.Balance.ShouldBe(Lira(70m));
        _ayse.Balance.ShouldBe(Lira(30m));
    }

    [Fact]
    public void Insufficient_funds_fail_and_change_no_balance()
    {
        Post(Debit(_cash, 100m), Credit(_ahmet, 100m));

        // The fee posting comes first and would succeed on its own; the whole entry must still be rejected.
        var result = Post(Debit(_ahmet, 1m), Credit(_feeIncome, 1m), Debit(_ahmet, 100m), Credit(_ayse, 100m));

        result.Error.ShouldBe(LedgerErrors.InsufficientFunds(_ahmet.Id));
        _ahmet.Balance.ShouldBe(Lira(100m));
        _ayse.Balance.ShouldBe(Money.Zero(Currency.TRY));
        _feeIncome.Balance.ShouldBe(Money.Zero(Currency.TRY));
    }

    [Fact]
    public void Internal_accounts_may_go_negative_customer_accounts_may_not()
    {
        // The bank pays a fee refund to Ahmet before any fee income exists: the income account goes negative.
        var refund = Post(Debit(_feeIncome, 5m), Credit(_ahmet, 5m));

        // Ayse has nothing, so she cannot pay.
        var overdraw = Post(Debit(_ayse, 0.01m), Credit(_ahmet, 0.01m));

        refund.IsSuccess.ShouldBeTrue();
        _feeIncome.Balance.ShouldBe(Lira(-5m));
        overdraw.Error.ShouldBe(LedgerErrors.InsufficientFunds(_ayse.Id));
    }

    [Fact]
    public void Posting_to_an_account_in_another_currency_fails()
    {
        var dollarAccount = Account.OpenCustomerAccount(Currency.USD);
        var entry = JournalEntry.Create(Guid.CreateVersion7(), [Debit(_cash, 10m), Credit(dollarAccount, 10m)]);

        var result = PostingEngine.Post(entry, Accounts(_cash, dollarAccount));

        result.Error!.Code.ShouldBe("ledger.currency_mismatch");
        _cash.Balance.ShouldBe(Money.Zero(Currency.TRY));
    }

    [Fact]
    public void An_account_that_was_not_provided_is_a_bug()
    {
        var entry = JournalEntry.Create(Guid.CreateVersion7(), [Debit(_cash, 10m), Credit(_ahmet, 10m)]);

        Should.Throw<ArgumentException>(() => PostingEngine.Post(entry, Accounts(_cash)));
    }

    [Fact]
    public void The_books_stay_balanced_after_any_sequence_of_entries()
    {
        Post(Debit(_cash, 500m), Credit(_ahmet, 500m));
        Post(Debit(_ahmet, 120m), Credit(_ayse, 120m));
        Post(Debit(_ayse, 2.50m), Credit(_feeIncome, 2.50m));
        Post(Debit(_ahmet, 80m), Credit(_cash, 80m));

        // The accounting equation: what the bank holds equals what it owes plus what it earned.
        var debitSide = _cash.Balance;
        var creditSide = _ahmet.Balance + _ayse.Balance + _feeIncome.Balance;
        debitSide.ShouldBe(creditSide);
    }

    [Theory]
    [InlineData(52, true)]
    [InlineData(51, false)]
    public void A_transfer_with_a_fee_is_posted_as_one_entry(int ahmetsBalance, bool succeeds)
    {
        Post(Debit(_cash, ahmetsBalance), Credit(_ahmet, ahmetsBalance));

        var result = Post(Debit(_ahmet, 2m), Credit(_feeIncome, 2m), Debit(_ahmet, 50m), Credit(_ayse, 50m));

        result.IsSuccess.ShouldBe(succeeds);
        _ahmet.Balance.ShouldBe(succeeds ? Lira(0m) : Lira(ahmetsBalance));
        _ayse.Balance.ShouldBe(succeeds ? Lira(50m) : Lira(0m));
        _feeIncome.Balance.ShouldBe(succeeds ? Lira(2m) : Lira(0m));
    }

    private Result Post(params Posting[] postings)
    {
        var entry = JournalEntry.Create(Guid.CreateVersion7(), postings);
        return PostingEngine.Post(entry, Accounts(_cash, _feeIncome, _ahmet, _ayse));
    }

    private static Dictionary<AccountId, Account> Accounts(params Account[] accounts) =>
        accounts.ToDictionary(account => account.Id);

    private static Money Lira(decimal amount) => Money.Of(amount, Currency.TRY);

    private static Posting Debit(Account account, decimal amount) => new(account.Id, Direction.Debit, Money.Of(amount, Currency.TRY));

    private static Posting Credit(Account account, decimal amount) => new(account.Id, Direction.Credit, Money.Of(amount, Currency.TRY));
}
