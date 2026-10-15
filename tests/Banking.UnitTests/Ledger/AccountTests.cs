using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;

namespace Banking.UnitTests.Ledger;

public sealed class AccountTests
{
    [Fact]
    public void A_customer_account_is_a_liability_that_cannot_go_negative()
    {
        var account = Account.OpenCustomerAccount(Currency.TRY);

        account.Type.ShouldBe(AccountType.Liability);
        account.AllowsNegativeBalance.ShouldBeFalse();
        account.Balance.ShouldBe(Money.Zero(Currency.TRY));
    }

    [Theory]
    [InlineData(AccountType.Liability, Direction.Credit, 100)]
    [InlineData(AccountType.Liability, Direction.Debit, -100)]
    [InlineData(AccountType.Income, Direction.Credit, 100)]
    [InlineData(AccountType.Income, Direction.Debit, -100)]
    [InlineData(AccountType.Asset, Direction.Debit, 100)]
    [InlineData(AccountType.Asset, Direction.Credit, -100)]
    [InlineData(AccountType.Expense, Direction.Debit, 100)]
    [InlineData(AccountType.Expense, Direction.Credit, -100)]
    public void The_account_type_decides_which_side_increases_the_balance(AccountType type, Direction direction, int expectedEffect)
    {
        var account = Account.OpenInternalAccount(type, Currency.TRY);

        account.EffectOf(direction, Money.Of(100m, Currency.TRY)).ShouldBe(Money.Of(expectedEffect, Currency.TRY));
    }

    [Theory]
    [InlineData(AccountType.Asset)]
    [InlineData(AccountType.Expense)]
    [InlineData(AccountType.Income)]
    [InlineData(AccountType.Liability)]
    public void A_internal_account_can_go_negative(AccountType type)
    {
        var account = Account.OpenInternalAccount(type, Currency.TRY);
        account.AllowsNegativeBalance.ShouldBeTrue();
        account.Balance.ShouldBe(Money.Zero(Currency.TRY));
    }
}
